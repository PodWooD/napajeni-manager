# Napajeni Manager - zmeri dopad stropu vykonu procesoru a doporuci hodnotu.
# Na konec vypise radek DOPORUCENO=<cislo>, ktery si precte aplikace.

param(
    [string]$PlanUspora,
    [string]$PlanBezny,
    [int[]] $Hodnoty = @(10, 20, 30, 40, 50, 60, 70, 80, 90),
    [int]   $MinTakt = 500,     # MHz - pod tim uz byva problem se prihlasit
    [double]$MaxZpomaleni = 3.0
)

$ErrorActionPreference = 'SilentlyContinue'

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch { }

$MAXSTAV = 'bc5038f7-23e0-4960-96da-33abaf5935ec'
$SUB     = '54533251-82be-4824-96c1-47b60b740d00'

if (-not $PlanUspora -or -not $PlanBezny) { Write-Output "Chybí GUID schémat."; exit 1 }

# puvodni stav, aby slo vsechno vratit
$puvodniAktivni = ((powercfg /getactivescheme) -replace '.*GUID:\s*([0-9a-fA-F\-]{36}).*','$1').Trim()
$puvodniMax = 100
$m = (powercfg /query $PlanUspora $SUB $MAXSTAV) | Select-String 'Current AC Power Setting Index:\s+0x([0-9a-fA-F]+)'
if ($m) { $puvodniMax = [Convert]::ToInt32($m.Matches[0].Groups[1].Value, 16) }

function Takt {
    $t = Get-CimInstance Win32_PerfFormattedData_Counters_ProcessorInformation -Filter "Name='_Total'"
    if ($t) { return [int][math]::Round($t.ProcessorFrequency * $t.PercentProcessorPerformance / 100, 0) }
    return 0
}

function DobaVypoctu {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $x = 0.0
    for ($i = 0; $i -lt 400000; $i++) { $x = [math]::Sqrt($i) + $x * 0.5 }
    $sw.Stop()
    return [int]$sw.ElapsedMilliseconds
}

# referencni mereni na beznem rezimu
powercfg /setactive $PlanBezny | Out-Null
Start-Sleep -Seconds 3
$refTakt = Takt
$refCas  = DobaVypoctu
if ($refCas -le 0) { $refCas = 1 }

$vysledky = @()
foreach ($h in ($Hodnoty | Sort-Object)) {
    powercfg /setacvalueindex $PlanUspora $SUB $MAXSTAV $h | Out-Null
    powercfg /setdcvalueindex $PlanUspora $SUB $MAXSTAV $h | Out-Null
    powercfg /setactive $PlanUspora | Out-Null
    Start-Sleep -Seconds 3
    $t = Takt
    $c = DobaVypoctu
    $vysledky += [PSCustomObject]@{
        Procent    = $h
        Takt       = $t
        Zpomaleni  = [math]::Round($c / $refCas, 1)
        Vyhovuje   = ($t -ge $MinTakt -and ($c / $refCas) -le $MaxZpomaleni)
    }
}

# navrat puvodniho stavu
powercfg /setacvalueindex $PlanUspora $SUB $MAXSTAV $puvodniMax | Out-Null
powercfg /setdcvalueindex $PlanUspora $SUB $MAXSTAV $puvodniMax | Out-Null
if ($puvodniAktivni) { powercfg /setactive $puvodniAktivni | Out-Null }

# vyber doporuceni - nejnizsi hodnota, ktera jeste vyhovuje
$vhodne = $vysledky | Where-Object { $_.Vyhovuje } | Sort-Object Procent
if ($vhodne) { $doporuceno = $vhodne[0].Procent; $duvod = 'nejúspornější nastavení, které zůstává použitelné' }
else {
    $nejlepsi = $vysledky | Sort-Object Takt -Descending | Select-Object -First 1
    $doporuceno = [math]::Min(100, $nejlepsi.Procent + 20)
    $duvod = 'žádná z měřených hodnot nebyla dost svižná, volím bezpečnější'
}

$dopRadek = $vysledky | Where-Object { $_.Procent -eq $doporuceno } | Select-Object -First 1

# ---------- vypis ----------
if ($dopRadek) {
    Write-Output ("V úsporném režimu poběží počítač zhruba {0}× pomaleji ({1} MHz)," -f $dopRadek.Zpomaleni, $dopRadek.Takt)
    Write-Output "ale zůstane plně ovladatelný — přihlášení i vzdálený přístup budou svižné."
} else {
    Write-Output "Nastaveno nejúspornější použitelné omezení."
}
Write-Output ""
Write-Output "Podrobnosti měření:"
Write-Output ("  {0,-8} {1,-11} {2,-11} {3}" -f 'Omezení', 'Takt', 'Zpomalení', '')
Write-Output ("  {0,-8} {1,-11} {2,-11} {3}" -f '--------', '-----------', '-----------', '')
Write-Output ("  {0,5} %  {1,6} MHz  {2,10}  {3}" -f 100, $refTakt, 'plný výkon', 'běžný režim')
foreach ($v in $vysledky) {
    $znacka = if ($v.Procent -eq $doporuceno) { '  ← nastaveno' } elseif (-not $v.Vyhovuje) { '  příliš pomalé' } else { '' }
    Write-Output ("  {0,5} %  {1,6} MHz  {2,9}×  {3}" -f $v.Procent, $v.Takt, $v.Zpomaleni, $znacka)
}
Write-Output "DOPORUCENO=$doporuceno"

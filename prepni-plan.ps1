# Napajeni Manager - prepinani rezimu napajeni
# Vsechna nastaveni cte z config.ini ve stejne slozce.
#
#   prepni-plan.ps1 -Akce Uspora  -Rezim Nocni
#   prepni-plan.ps1 -Akce Uspora  -Rezim Zamknuti
#   prepni-plan.ps1 -Akce Bezny   -Rezim Odemknuti
#   prepni-plan.ps1 -Akce Uspora                    (rucni test, s oknem)

param(
    [ValidateSet('Uspora','Bezny')] [string]$Akce = 'Uspora',
    [ValidateSet('Nocni','Vikend','Zamknuti','Odemknuti','Rucni')] [string]$Rezim = 'Rucni'
)

$slozka = Split-Path -Parent $MyInvocation.MyCommand.Path
$log    = Join-Path $slozka 'prepni-plan.log'

function Zapis($t) { "$(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')  $t" | Out-File $log -Append -Encoding utf8 }

# ---------- config ----------
function NactiConfig {
    $c = @{}
    $f = Join-Path $slozka 'config.ini'
    if (Test-Path $f) {
        foreach ($r in (Get-Content $f -Encoding UTF8)) {
            $r = $r.Trim()
            if ($r.Length -eq 0 -or $r.StartsWith('#')) { continue }
            $i = $r.IndexOf('=')
            if ($i -gt 0) { $c[$r.Substring(0,$i).Trim()] = $r.Substring($i+1).Trim() }
        }
    }
    return $c
}
$cfg = NactiConfig
function C($k, $vych) { if ($cfg.ContainsKey($k) -and $cfg[$k] -ne '') { return $cfg[$k] } else { return $vych } }
function CI($k, $vych) { $v = 0; if ([int]::TryParse((C $k ''), [ref]$v)) { return $v } else { return $vych } }
function CB($k) { return (C $k '0') -eq '1' }

$guidUspora = C 'PlanUspora' 'a1841308-3541-4fab-bc81-f71556f20b4a'
$guidBezny  = C 'PlanBezny'  '381b4222-f694-41f0-9685-ff5bb260df2e'

# ---------- parametry podle rezimu ----------
switch ($Rezim) {
    'Nocni'     { $kolKlidu = CI 'Tyden_KolKlidu' 3;    $interval = CI 'Tyden_IntervalS' 300
                  $vypnoutTV = CB 'Tyden_VypnoutTV';    $zamknout = CB 'Tyden_Zamknout'
                  $cekat = $true;  $tiche = $false; $jenZamceno = $false }
    'Vikend'    { $kolKlidu = CI 'Vikend_KolKlidu' 3;   $interval = CI 'Vikend_IntervalS' 300
                  $vypnoutTV = CB 'Vikend_VypnoutTV';   $zamknout = CB 'Vikend_Zamknout'
                  $cekat = $true;  $tiche = $false; $jenZamceno = $false }
    'Zamknuti'  { $kolKlidu = CI 'Zamknuti_KolKlidu' 2; $interval = CI 'Zamknuti_IntervalS' 180
                  $vypnoutTV = CB 'Zamknuti_VypnoutTV'; $zamknout = $false
                  $cekat = $true;  $tiche = $true;  $jenZamceno = $true }
    'Odemknuti' { $kolKlidu = 0; $interval = 0; $vypnoutTV = $false; $zamknout = $false
                  $cekat = $false; $tiche = $true;  $jenZamceno = $false }
    default     { $kolKlidu = 0; $interval = 0; $vypnoutTV = $false; $zamknout = $false
                  $cekat = $false; $tiche = $false; $jenZamceno = $false }
}

$prahCPU = CI 'PrahCPU' 20
$prahGPU = CI 'PrahGPU' 20
$odpocet = CI 'Odpocet' 60

$cilovyGuid  = if ($Akce -eq 'Uspora') { $guidUspora } else { $guidBezny }
$cilovyNazev = ((powercfg /list) | Select-String ([regex]::Escape($cilovyGuid))) -replace '.*\((.*)\).*','$1'
if (-not $cilovyNazev) { $cilovyNazev = $Akce }

# ---------- pomocne ----------
function JeZamceno { return ((Get-Process LogonUI -ErrorAction SilentlyContinue) -ne $null) }

function VytizeniCPU {
    try {
        $t = Get-CimInstance Win32_PerfFormattedData_Counters_ProcessorInformation -Filter "Name='_Total'" -ErrorAction Stop
        return [int]$t.PercentProcessorTime
    } catch { return 0 }
}

function VytizeniGPU {
    $smi = Join-Path $env:SystemRoot 'System32\nvidia-smi.exe'
    if (Test-Path $smi) {
        try {
            $o = & $smi --query-gpu=utilization.gpu --format=csv,noheader,nounits 2>$null
            if ($o -match '(\d+)') { return [int]$Matches[1] }
        } catch { }
    }
    return 0
}

function Prepni {
    & powercfg /setactive $cilovyGuid
    if ($LASTEXITCODE -eq 0) { Zapis "PREPNUTO na '$cilovyNazev' (rezim: $Rezim)" }
    else { Zapis "CHYBA pri prepinani (exit $LASTEXITCODE)" }

    if ($vypnoutTV -and (CB 'TV_Povoleno')) {
        $tv = Join-Path $slozka 'lg-tv.ps1'
        if (Test-Path $tv) {
            try { $v = & $tv -Vypnout 2>&1; Zapis "  TV: $v" }
            catch { Zapis "  TV: chyba - $($_.Exception.Message)" }
        }
    }

    if ($zamknout) {
        if (JeZamceno) { Zapis "  (relace uz zamcena)" }
        else { Zapis "  zamykam stanici"; & rundll32.exe user32.dll,LockWorkStation }
    }
}

# ---------- nastaveni stropu procesoru pro usporny plan ----------
if ($Akce -eq 'Uspora') {
    $max = CI 'MaxStavProcesoru' 50
    & powercfg /setacvalueindex $guidUspora SUB_PROCESSOR bc5038f7-23e0-4960-96da-33abaf5935ec $max 2>$null
    & powercfg /setdcvalueindex $guidUspora SUB_PROCESSOR bc5038f7-23e0-4960-96da-33abaf5935ec $max 2>$null
}

# ---------- zakaz uspavani ----------
if (CB 'ZakazatSpanek') {
    foreach ($g in @($guidUspora, $guidBezny)) {
        & powercfg /setacvalueindex $g SUB_SLEEP STANDBYIDLE 0 2>$null
        & powercfg /setacvalueindex $g SUB_SLEEP HIBERNATEIDLE 0 2>$null
    }
}

# ---------- jen kdyz je zamceno ----------
if ($jenZamceno -and -not (JeZamceno)) {
    Zapis "NEPREPNUTO - relace neni zamcena (rezim: $Rezim)"
    exit
}

# ---------- cekani na klid ----------
if ($cekat -and $kolKlidu -gt 0) {
    $ted = Get-Date
    $deadline = if ($ted.Hour -lt 7) { $ted.Date.AddHours(7) } else { $ted.Date.AddDays(1).AddHours(7) }
    Zapis "CEKANI NA KLID ($Rezim): CPU<$prahCPU% a GPU<$prahGPU%, ${kolKlidu}x po $interval s, nejpozdeji do $($deadline.ToString('HH:mm'))"

    $klid = 0
    while ((Get-Date) -lt $deadline) {
        if ($jenZamceno -and -not (JeZamceno)) { Zapis "NEPREPNUTO - uzivatel se vratil behem cekani"; exit }
        $c = VytizeniCPU
        $g = VytizeniGPU
        if ($c -lt $prahCPU -and $g -lt $prahGPU) {
            $klid++
            Zapis "  klid ($klid/$kolKlidu)  CPU=$c%  GPU=$g%"
            if ($klid -ge $kolKlidu) { break }
        } else {
            if ($klid -gt 0) { Zapis "  zatez se vratila, pocitadlo zpet na 0" }
            $klid = 0
            Zapis "  ceka se: CPU=$c%  GPU=$g%"
        }
        Start-Sleep -Seconds $interval
    }

    if ($klid -lt $kolKlidu) { Zapis "NEPREPNUTO - system byl zatizeny az do $($deadline.ToString('HH:mm'))"; exit }
    if ($jenZamceno -and -not (JeZamceno)) { Zapis "NEPREPNUTO - uzivatel se vratil tesne pred prepnutim"; exit }
    Zapis "System v klidu -> pokracuji"
}

# ---------- tichy rezim ----------
if ($tiche) { Prepni; exit }

# ---------- okno s odpoctem ----------
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$form = New-Object System.Windows.Forms.Form
$form.Text            = 'Změna režimu napájení'
$form.Size            = New-Object System.Drawing.Size(480, 230)
$form.StartPosition   = 'CenterScreen'
$form.TopMost         = $true
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox     = $false
$form.MinimizeBox     = $false
$form.BackColor       = [System.Drawing.Color]::FromArgb(32, 32, 32)
$form.ForeColor       = [System.Drawing.Color]::White

$nadpis = New-Object System.Windows.Forms.Label
$nadpis.Text      = "Přepínám na: $cilovyNazev"
$nadpis.Font      = New-Object System.Drawing.Font('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)
$nadpis.Size      = New-Object System.Drawing.Size(440, 34)
$nadpis.Location  = New-Object System.Drawing.Point(20, 22)
$nadpis.TextAlign = 'MiddleCenter'
$form.Controls.Add($nadpis)

$lblOdpocet = New-Object System.Windows.Forms.Label
$lblOdpocet.Font      = New-Object System.Drawing.Font('Segoe UI', 26, [System.Drawing.FontStyle]::Bold)
$lblOdpocet.ForeColor = [System.Drawing.Color]::FromArgb(255, 170, 60)
$lblOdpocet.Size      = New-Object System.Drawing.Size(440, 52)
$lblOdpocet.Location  = New-Object System.Drawing.Point(20, 62)
$lblOdpocet.TextAlign = 'MiddleCenter'
$lblOdpocet.Text      = "$odpocet s"
$form.Controls.Add($lblOdpocet)

$btn = New-Object System.Windows.Forms.Button
$btn.Text      = 'Zrušit (nechat současný režim)'
$btn.Size      = New-Object System.Drawing.Size(260, 38)
$btn.Location  = New-Object System.Drawing.Point(110, 128)
$btn.FlatStyle = 'Flat'
$btn.BackColor = [System.Drawing.Color]::FromArgb(64, 64, 64)
$form.Controls.Add($btn)

$script:zruseno = $false
$btn.Add_Click({ $script:zruseno = $true; $form.Close() })

$script:zbyva = $odpocet
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000
$timer.Add_Tick({
    $script:zbyva--
    $lblOdpocet.Text = "$($script:zbyva) s"
    if ($script:zbyva -le 0) { $timer.Stop(); $form.Close() }
})
$timer.Start()

[void]$form.ShowDialog()
$timer.Stop()

if ($script:zruseno) { Zapis "ZRUSENO uzivatelem (rezim: $Rezim)" } else { Prepni }

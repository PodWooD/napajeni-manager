# Napajeni Manager - vytvoreni/odebrani naplanovanych uloh podle config.ini
#
#   nastav-ulohy.ps1            -> podle configu vytvori nebo odebere jednotlive ulohy
#   nastav-ulohy.ps1 -Odebrat   -> odebere vsechny ulohy programu

param([switch]$Odebrat)

$slozka = Split-Path -Parent $MyInvocation.MyCommand.Path
$skript = Join-Path $slozka 'prepni-plan.ps1'
$log    = Join-Path $slozka 'ulohy.log'
$predpona = 'NapajeniManager-'

function Zapis($t) { "$(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')  $t" | Out-File $log -Append -Encoding utf8 }

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

$uzivatel = "$env:USERDOMAIN\$env:USERNAME"

function Smaz($nazev) {
    try { Unregister-ScheduledTask -TaskName $nazev -Confirm:$false -ErrorAction Stop; Zapis "odebrano: $nazev" } catch { }
}

function Vytvor($nazev, $trigger, $argy, $popis, $limitH) {
    Smaz $nazev
    $akce = New-ScheduledTaskAction -Execute 'powershell.exe' `
            -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$skript`" $argy"
    $nast = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
            -ExecutionTimeLimit ([TimeSpan]::FromHours($limitH)) -MultipleInstances IgnoreNew
    $nast.DisallowStartIfOnBatteries = $false
    $princ = New-ScheduledTaskPrincipal -UserId $uzivatel -LogonType Interactive -RunLevel Limited
    try {
        Register-ScheduledTask -TaskName $nazev -Action $akce -Trigger $trigger -Settings $nast `
            -Principal $princ -Description $popis -ErrorAction Stop | Out-Null
        Zapis "vytvoreno: $nazev  ($argy)"
    } catch { Zapis "CHYBA u $nazev : $($_.Exception.Message)" }
}

function TriggerRelace($stateChange) {
    $t = New-CimInstance -CimClass (Get-CimClass -ClassName MSFT_TaskSessionStateChangeTrigger `
         -Namespace Root/Microsoft/Windows/TaskScheduler) -ClientOnly
    $t.StateChange = $stateChange     # 3 = vzdalene pripojeni, 7 = zamknuti, 8 = odemknuti
    $t.UserId      = $uzivatel
    $t.Enabled     = $true
    return $t
}

# ---------- odebrani vseho ----------
if ($Odebrat) {
    Zapis "=== ODEBIRAM VSECHNY ULOHY ==="
    Get-ScheduledTask -TaskName "$predpona*" -ErrorAction SilentlyContinue | ForEach-Object { Smaz $_.TaskName }
    Zapis "hotovo"
    exit
}

Zapis "=== NASTAVUJI ULOHY PODLE CONFIGU ==="

# ---------- vsedni dny ----------
if ((CB 'Tyden_Povoleno') -and (C 'Tyden_Dny' '') -ne '') {
    $dny = (C 'Tyden_Dny' '').Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
    Vytvor "${predpona}Uspora-Tyden" `
        (New-ScheduledTaskTrigger -Weekly -DaysOfWeek $dny -At (C 'Tyden_Cas' '00:00')) `
        '-Akce Uspora -Rezim Nocni' 'Usporny rezim ve vsedni dny' 8
} else { Smaz "${predpona}Uspora-Tyden" }

# ---------- vikend ----------
if ((CB 'Vikend_Povoleno') -and (C 'Vikend_Dny' '') -ne '') {
    $dny = (C 'Vikend_Dny' '').Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
    Vytvor "${predpona}Uspora-Vikend" `
        (New-ScheduledTaskTrigger -Weekly -DaysOfWeek $dny -At (C 'Vikend_Cas' '02:00')) `
        '-Akce Uspora -Rezim Vikend' 'Usporny rezim o vikendu' 8
} else { Smaz "${predpona}Uspora-Vikend" }

# ---------- po zamknuti ----------
if (CB 'Zamknuti_Povoleno') {
    Vytvor "${predpona}Uspora-Zamknuti" (TriggerRelace 7) `
        '-Akce Uspora -Rezim Zamknuti' 'Usporny rezim po zamknuti a utichnuti systemu' 8
} else { Smaz "${predpona}Uspora-Zamknuti" }

# ---------- po odemknuti ----------
if (CB 'Odemknuti_Povoleno') {
    # Osmicka sama nestaci. Kdyz se klient RDP pripoji s predanymi prihlasovacimi
    # udaji, relace se obnovi bez zamykaci obrazovky a udalost odemknuti neprijde.
    # Trojka (vzdalene pripojeni) tenhle pripad pokryje. Kdyz prijdou obe,
    # druhy beh nic nedela - Prepni() pozna, ze cilovy plan uz je aktivni.
    Vytvor "${predpona}Bezny-Odemknuti" @((TriggerRelace 8), (TriggerRelace 3)) `
        '-Akce Bezny -Rezim Odemknuti' 'Navrat do bezneho rezimu po odemknuti nebo vzdalenem pripojeni' 1
} else { Smaz "${predpona}Bezny-Odemknuti" }

Zapis "hotovo"

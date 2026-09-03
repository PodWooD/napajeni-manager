# Napajeni Manager - ovladani televize LG (webOS) pres SSAP
#
#   lg-tv.ps1 -Hledat     -> najde televizi na siti
#   lg-tv.ps1 -Sparovat   -> jednorazove parovani (potvrdit dalkovym ovladacem)
#   lg-tv.ps1 -Vypnout    -> uvede televizi do pohotovostniho rezimu
#   lg-tv.ps1 -Stav       -> overi dostupnost

param(
    [string]$IP,
    [int]$Port,
    [switch]$Hledat,
    [switch]$Sparovat,
    [switch]$Vypnout,
    [switch]$Stav
)

$slozka = Split-Path -Parent $MyInvocation.MyCommand.Path
$log    = Join-Path $slozka 'lg-tv.log'
$klicSoubor = Join-Path $slozka 'lg-tv-key.txt'

# vystup cte volajici aplikace jako UTF-8
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch { }

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
if (-not $IP)   { $IP   = if ($cfg.ContainsKey('TV_IP'))   { $cfg['TV_IP'] }   else { '' } }
if (-not $Port) { $Port = if ($cfg.ContainsKey('TV_Port')) { [int]$cfg['TV_Port'] } else { 3001 } }
if ($Port -le 0) { $Port = 3001 }

# ---------- hledani na siti ----------
if ($Hledat) {
    $moje = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notmatch '^(127\.|169\.254\.)' -and $_.PrefixLength -ge 22 }
    $kandidati = @()
    foreach ($a in $moje) {
        $zaklad = ($a.IPAddress -split '\.')[0..2] -join '.'
        $kandidati += (Get-NetNeighbor -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.State -in 'Reachable','Stale','Permanent' -and $_.IPAddress -like "$zaklad.*" } |
            Select-Object -ExpandProperty IPAddress)
    }
    $kandidati = $kandidati | Sort-Object -Unique
    foreach ($ip in $kandidati) {
        $jmeno = ''
        try { $jmeno = [System.Net.Dns]::GetHostEntry($ip).HostName } catch { }
        $otevreny = $false
        $t = New-Object System.Net.Sockets.TcpClient
        try {
            $a = $t.BeginConnect($ip, 3001, $null, $null)
            $otevreny = ($a.AsyncWaitHandle.WaitOne(400, $false) -and $t.Connected)
        } catch { } finally { $t.Close() }
        if ($otevreny -and ($jmeno -match 'LG|webOS' -or $jmeno -eq '')) {
            Write-Output "Nalezeno: $ip  $jmeno"
            Zapis "HLEDANI: nalezeno $ip ($jmeno)"
            exit
        }
    }
    Write-Output "Televize nenalezena. Zkontrolujte, ze je zapnuta a na stejne siti."
    exit
}

if (-not $IP) { Write-Output "Neni zadana IP adresa televize."; exit 1 }

function Dostupna {
    $t = New-Object System.Net.Sockets.TcpClient
    try {
        $a = $t.BeginConnect($IP, $Port, $null, $null)
        return ($a.AsyncWaitHandle.WaitOne(1500, $false) -and $t.Connected)
    } catch { return $false } finally { $t.Close() }
}

if ($Stav) {
    if (Dostupna) { Write-Output "TV $IP : dostupna na siti (port $Port)" }
    else { Write-Output "TV $IP : nedostupna" }
    exit
}

if (-not (Dostupna)) { Zapis "TV $IP nedostupna"; Write-Output "TV nedostupna"; exit }

# ---------- WebSocket ----------
# webOS 2022+ vyzaduje wss:// na portu 3001 se self-signed certifikatem.
# PowerShell scriptblock jako callback nefunguje (bezi na jinem vlakne) -> kompilovana trida.
if (-not ('CertBypass' -as [type])) {
    Add-Type @"
using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public static class CertBypass {
    public static void Enable() {
        ServicePointManager.ServerCertificateValidationCallback =
            delegate(object s, X509Certificate c, X509Chain ch, SslPolicyErrors e) { return true; };
        ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
    }
}
"@
}
[CertBypass]::Enable()

$ws  = New-Object System.Net.WebSockets.ClientWebSocket
$cts = New-Object System.Threading.CancellationTokenSource
$cts.CancelAfter(60000)

function Odesli($obj) {
    $json  = $obj | ConvertTo-Json -Depth 12 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $seg   = New-Object System.ArraySegment[byte] -ArgumentList @(,$bytes)
    $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait()
}

function Prijmi([int]$timeoutMs = 45000) {
    $buf = New-Object byte[] 16384
    $seg = New-Object System.ArraySegment[byte] -ArgumentList @(,$buf)
    $t   = New-Object System.Threading.CancellationTokenSource
    $t.CancelAfter($timeoutMs)
    $sb  = New-Object System.Text.StringBuilder
    do {
        $r = $ws.ReceiveAsync($seg, $t.Token).GetAwaiter().GetResult()
        [void]$sb.Append([System.Text.Encoding]::UTF8.GetString($buf, 0, $r.Count))
    } while (-not $r.EndOfMessage)
    return $sb.ToString()
}

$schema = if ($Port -eq 3001) { 'wss' } else { 'ws' }
try { $ws.ConnectAsync([Uri]"${schema}://${IP}:${Port}", $cts.Token).Wait() }
catch { Zapis "CHYBA pripojeni: $($_.Exception.Message)"; Write-Output "Nepodarilo se pripojit."; exit 1 }

$klic = if (Test-Path $klicSoubor) { (Get-Content $klicSoubor -Raw).Trim() } else { '' }

$manifest = @{
    manifestVersion = 1
    appVersion      = '1.1'
    signed = @{
        created  = '20140509'
        appId    = 'com.lge.test'
        vendorId = 'com.lge'
        localizedAppNames    = @{ '' = 'LG Remote App' }
        localizedVendorNames = @{ '' = 'LG Electronics' }
        permissions = @('TEST_SECURE','CONTROL_INPUT_TEXT','CONTROL_MOUSE_AND_KEYBOARD','READ_INSTALLED_APPS','READ_LGE_SDX','READ_NOTIFICATIONS','SEARCH','WRITE_SETTINGS','WRITE_NOTIFICATION_ALERT','CONTROL_POWER','READ_CURRENT_CHANNEL','READ_RUNNING_APPS','READ_UPDATE_INFO','UPDATE_FROM_REMOTE_APP','READ_LGE_TV_INPUT_EVENTS','READ_TV_CURRENT_TIME')
        serial = '2f930e2d2cfe083771f68e4fe7bb07'
    }
    permissions = @('LAUNCH','LAUNCH_WEBAPP','APP_TO_APP','CLOSE','TEST_OPEN','TEST_PROTECTED','CONTROL_AUDIO','CONTROL_DISPLAY','CONTROL_INPUT_JOYSTICK','CONTROL_INPUT_MEDIA_RECORDING','CONTROL_INPUT_MEDIA_PLAYBACK','CONTROL_INPUT_TV','CONTROL_POWER','READ_APP_STATUS','READ_CURRENT_CHANNEL','READ_INPUT_DEVICE_LIST','READ_NETWORK_STATE','READ_RUNNING_APPS','READ_TV_CHANNEL_LIST','WRITE_NOTIFICATION_TOAST','READ_POWER_STATE','READ_COUNTRY_INFO')
}

$reg = @{ type = 'register'; id = 'register_0'; payload = @{ forcePairing = $false; pairingType = 'PROMPT'; manifest = $manifest } }
if ($klic) { $reg.payload['client-key'] = $klic }
Odesli $reg

if (-not $klic) { Write-Output "Na televizi se objevil dotaz - potvrdte ho dalkovym ovladacem (45 s)." }

$ok = $false
for ($i = 0; $i -lt 3; $i++) {
    $r = Prijmi 45000
    if ($r -match '"type"\s*:\s*"registered"') {
        if ($r -match '"client-key"\s*:\s*"([^"]+)"') {
            $novy = $Matches[1]
            if ($novy -ne $klic) { $novy | Out-File $klicSoubor -Encoding ascii -NoNewline; Zapis "ulozen novy klic" }
        }
        $ok = $true; break
    }
    if ($r -match '"type"\s*:\s*"error"') { Zapis "CHYBA registrace: $r"; Write-Output "Registrace odmitnuta."; $ws.Dispose(); exit 1 }
}

if (-not $ok) { Zapis "registrace nedokoncena"; Write-Output "Nepotvrzeno na televizi."; $ws.Dispose(); exit 1 }
Zapis "registrace OK"

if ($Sparovat) { Write-Output "Sparovano, klic ulozen."; $ws.Dispose(); exit }

if ($Vypnout) {
    Odesli @{ type = 'request'; id = 'off_1'; uri = 'ssap://system/turnOff' }
    Start-Sleep -Milliseconds 800
    Zapis "odeslan prikaz k vypnuti ($IP)"
    Write-Output "Prikaz k vypnuti odeslan."
}

$ws.Dispose()

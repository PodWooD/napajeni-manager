# Analýza projektu Napájení Manager 1.1

Autor analýzy: Claude Fable 5.1
Datum: 4. 9. 2026
Stav repozitáře: větev `main`, commit `467a557` (Verze 1.1), tagy `v1.0`, `v1.1`

---

## 1. Shrnutí

Malý, čistě udělaný projekt s jasnou architekturou: WinForms okno jen zapisuje `config.ini`
a zakládá úlohy v Plánovači, vlastní práci dělají čtyři PowerShell skripty. Kód je čitelný,
komentáře vysvětlují „proč“, README je nadprůměrné. Aplikace je nasazená a funguje: čtyři úlohy
ve stavu Ready, log ukazuje správné čekání na klid, přepnutí i návrat.

| Kontrola | Výsledek |
|---|---|
| Kompilace `csc /warn:4` | OK, 0 varování, 58 KB |
| Parser všech 4 skriptů `.ps1` | 0 chyb, UTF-8 BOM všude |
| Naplánované úlohy | 4× Ready |
| Chyby v lozích | 1 reálná (TV WebSocket, viz 2.1) |

Externí posudek v `analyza-grok.md` už řadu věcí vytkl a dnešní commity je opravily:
neuložené změny, doba klidu v minutách, živý stav v okně, nezamrzání při měření, DPI,
no-op při opakovaném odemknutí. Níže je to, co zbývá, plus věci, které posudek neviděl.

---

## 2. Chyby a rizika (podle dopadu)

### 2.1 Vypnutí TV padá po `turnOff`
Log `prepni-plan.log` z 01:35:

```
TV: chyba - Exception calling "GetResult" ... Objekt WebSocket je pro tuto operaci
v neplatném stavu (CloseReceived). Platné stavy jsou: Open, CloseSent
```

Televize po příkazu zavře socket, skript dál volá `ReceiveAsync` a hlásí chybu, i když TV
zhasla. V `lg-tv.ps1` chybí kontrola `$ws.State -eq 'Open'` před čtením a `$ws.Dispose()`
v bloku `finally`.

### 2.2 Noční úloha může zpomalit počítač ráno, když u něj sedíte
Režim `Nocni` nemá `jenZamceno`, čeká až 8 h na klid a nekontroluje ani odemknutí, ani
nečinnost uživatele. Když v 6:30 čtete e-maily s nízkým CPU, vyskočí odpočet.
Řešení: v nočním režimu hlídat i dobu od posledního vstupu (`GetLastInputInfo`) a přerušit
čekání, pokud mezitím proběhlo odemknutí (nebo přepnutí na běžný režim).

### 2.3 Chybí ranní návrat na plný výkon
`nastav-ulohy.ps1` zakládá jen `Uspora-Tyden`, `Uspora-Vikend`, `Uspora-Zamknuti`
a `Bezny-Odemknuti`. Jediná cesta zpět je odemknutí nebo RDP připojení. Bez interakce zůstane
stroj zpomalený celý den.

### 2.4 Deadline 7:00 je natvrdo ve skriptu
`prepni-plan.ps1:142`:

```powershell
$deadline = if ($ted.Hour -lt 7) { $ted.Date.AddHours(7) } else { $ted.Date.AddDays(1).AddHours(7) }
```

Koliduje s `-ExecutionTimeLimit 8h`: noční čas 22:00 znamená deadline za 9 h, Plánovač úlohu
zabije v 6:00 bez zápisu do logu. Čas patří do `config.ini` a do okna.

### 2.5 Strop CPU a zákaz spánku se zapíšou až při prvním přepnutí
`prepni-plan.ps1:118-131` volá `powercfg /setacvalueindex` až za běhu úlohy. Uživatel změří
40 %, uloží, a do noci se v `powercfg` nic nezmění. Stačí stejný blok zavolat
z `nastav-ulohy.ps1` při uložení.

### 2.6 UI vlákno se stále blokuje
- `Uloz()` volá `SpustPSSync` s limitem 90 s na vlákně okna.
- Spárovat a Vyzkoušet vypnutí volají `SpustPS(..., true)` s `WaitForExit(180000)`;
  uživatel mezitím musí potvrdit na dálkovém ovladači (45 s) a okno nereaguje.
- Výstup těchto skriptů se nikam nezobrazí (`WindowStyle Hidden`, bez přesměrování),
  úspěch spárování poznáte jen podle stavového pruhu.

### 2.7 Časový limit v `SpustPSVystup` nefunguje
`NapajeniManager.cs:1097-1104`: `ReadToEnd()` běží před `WaitForExit(timeout)`, takže se čeká
do konce skriptu bez ohledu na limit. Exit kódy skriptů se nikde nekontrolují, hláška
„Nastavení uloženo a úlohy přenastaveny“ může lhát.

### 2.8 Bez mutexu
Lze otevřít dvě okna a přepsat si `config.ini` navzájem, případně dvakrát zakládat úlohy.

### 2.9 Práh GPU se nabízí i bez NVIDIA
`VytizeniGPU` bez `nvidia-smi` vrací vždy 0, práh v nastavení pak nic nedělá.
`nvidia-smi.exe` se hledá jen v `System32`.

### 2.10 Bez validace vstupů
IP adresa a port televize se ukládají bez kontroly (`Uloz()`, `UlozTV()`).

### 2.11 Logy rostou bez omezení
Při čekání na klid přibývá řádek každých 3 až 5 minut, žádná rotace.

---

## 3. Kvalita kódu a repozitář

### Verze a metadata
- Instalátor 1.1, manifest `1.0.0.0`, EXE `0.0.0.0` (chybí `AssemblyVersion`, `AssemblyTitle`,
  `AssemblyDescription`).
- EXE nemá ikonu (`/win32icon` v `build.cmd`), v nabídce Start je generická.
- `installer.iss` má `MinVersion=10.0`, manifest deklaruje podporu Windows 7 až 11.

### Mrtvý kód (`Ui.cs`)
- Třída `Posuvnik` (řádky 169-220) se nikde nepoužívá. Byla zřejmě určena pro ruční nastavení
  stropu, které v UI dnes chybí.
- `Pisma.Velky`, pole `Tlacitko.Hlavni`.

### Drobné GDI úniky
- `NavPolozka.OnPaint` a `Lista()` vytvářejí `new Font(...)` při každém překreslení.
- `Karta.OnPaint` nedisposuje `SolidBrush`.

### Duplicita
Parser `config.ini` je 5× opsaný (4 skripty + C#). Stačí jeden `Common.ps1` dot-sourcovaný
ze všech skriptů, včetně funkce `Zapis`.

### Dokumentace zaostává za UI
- README popisuje pole „Kolikrát klid“, UI ukazuje minuty.
- `config.ini.vzor` má `IntervalS=300`, aplikace vždy zapíše 180.
- Délka měření: „minutu“ v README a v dialogu, „dvě minuty“ v okně.

### Proces
- Bez CI, testů a `CHANGELOG.md`.
- Logika hodná testů (`NaMinuty`/`NaPocet`, `NejblizsiDen`, `ParseCas`, parser configu)
  je zaklíněná ve třídě `HlavniOkno`.
- `.gitattributes` je správně; v lokálním pracovním stromu mají `.ps1` LF (nebylo provedeno
  `git add --renormalize`), v indexu je to v pořádku, čerstvý klon dostane CRLF.

### Hledání TV (`lg-tv.ps1 -Hledat`)
- Ignoruje nastavený `$Port`, natvrdo testuje 3001.
- Přijme libovolné zařízení s otevřeným portem a prázdným reverzním DNS.
- Prochází jen ARP sousedy, TV mimo tabulku se nenajde.

### Bezpečnost
V pořádku pro daný účel: žádný internet, argumenty pro `powercfg` se předávají odděleně,
obcházení certifikátu je jen v procesu pro TV, `-ExecutionPolicy Bypass` je u takových
nástrojů běžné, klíč TV v plaintextu je přijatelný.

---

## 4. Návrhy na zlepšení v doporučeném pořadí

1. **Opravit TV skript** – stav socketu před čtením, `Dispose` ve `finally`, výsledek
   spárování zobrazit v okně.
2. **Ranní návrat jako pátá úloha** se stejným časem jako deadline, oba nastavitelné v okně.
3. **Aplikovat strop a zákaz spánku při uložení**, ne až v noci.
4. **Noční režim: hlídat nečinnost uživatele a odemknutí**, ne jen CPU/GPU.
5. **Ruční posuvník stropu** vedle tlačítka Změřit (třída `Posuvnik` už existuje).
6. **Vše na pozadí** přes existující `NaPozadi`, včetně Uložit; kontrolovat exit kódy;
   opravit pořadí `ReadToEnd`/`WaitForExit`.
7. **Mutex, validace IP/portu, rotace logů** (např. oříznout na 2000 řádků při startu skriptu).
8. **Skrýt práh GPU bez NVIDIA**, případně přidat AMD/Intel přes čítač `GPU Engine`.
9. **Sjednotit verzi** do jednoho místa (AssemblyInfo, manifest, `installer.iss`),
   přidat ikonu a `CHANGELOG.md`.
10. **GitHub Actions**: build přes `csc` na `windows-latest`, ISCC, artefakt k release
    při pushi tagu.
11. **Společný `Common.ps1`** pro config a log, smazat mrtvý kód, srovnat README
    a `config.ini.vzor` s UI.
12. **Do budoucna**: ikona u hodin s rychlým přepnutím, výjimky procesů (nepřepínat, když běží
    `ffmpeg`), zapnutí TV při návratu (Wake-on-LAN), lokalizace EN.

---

## 5. Co bylo ověřeno

- Přečteny všechny zdrojové soubory (`NapajeniManager.cs`, `Ui.cs`, 4× `.ps1`,
  `installer.iss`, `build.cmd`, manifest, README, `config.ini.vzor`).
- Zkušební kompilace do dočasné složky s `/warn:4`.
- `[Parser]::ParseFile` na všech skriptech.
- Výstup `powercfg /list`, `/getactivescheme` a `/query` na tomto stroji (cs-CZ): popisky jsou
  anglické, regexy ve skriptech fungují i na české instalaci.
- `Get-ScheduledTask 'NapajeniManager-*'` a obsah `prepni-plan.log`, `ulohy.log`, `lg-tv.log`.

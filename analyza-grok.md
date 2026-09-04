# Analýza projektu — Napájení Manager

**Autor:** Grok (xAI)  
**Datum:** 4. 9. 2026  
**Rozsah:** kód (`NapajeniManager.cs`, `Ui.cs`), PowerShell skripty, instalátor, README, reálné logy (`prepni-plan.log`, `ulohy.log`, `lg-tv.log`) a `config.ini`.

Tento dokument je audit existujícího stavu a návrh dalšího směřování (UX, UI, funkce, spolehlivost). Není to implementační plán k okamžitému zápisu do kódu.

---

## 1. O čem projekt je

Automatické přepínání režimů napájení ve Windows s ohledem na to, co počítač zrovna dělá. Windows umí uspat, nebo neudělat nic. Napájení Manager počká, až bude systém skutečně nečinný, teprve pak sníží výkon (strop `PROCTHROTTLEMAX`) a při odemknutí ho vrátí.

Architektura:

| Vrstva | Role |
|---|---|
| `NapajeniManager.exe` (WinForms) | Nastavovací okno. Sama nic nehlídá. |
| `config.ini` | Jediný zdroj pravdy pro nastavení. |
| Plánovač úloh (`NapajeniManager-*`) | Spouští skripty v čase / při zámku / odemčení. |
| `prepni-plan.ps1` | Čekání na klid, odpočet, přepnutí, TV, zámek. |
| `nastav-ulohy.ps1` | Zakládá a ruší úlohy podle configu. |
| `zmer-vykon.ps1` | Změří dopad stropu a doporučí hodnotu. |
| `lg-tv.ps1` | LG webOS přes SSAP (`wss://…:3001`). |

To je pro tenhle problém správné rozhodnutí: žádná rezidentní služba, žádný správce, odinstalace umí úlohy uklidit.

---

## 2. Co už je silné

- Jasný produktový příběh, který Windows sám neřeší (render / kompilace / stahování přes noc).
- README je poctivé: požadavky, měření, Ryzen/CPPC, soukromí, troubleshooting včetně UTF-8 BOM.
- Instalace bez účtu správce (`PrivilegesRequired=lowest`, `%LOCALAPPDATA%\Programs\…`).
- Vlastní tmavý UI kit (`Karta`, `Prepinac`, `Tlacitko`, `Cislovac`, `NavPolozka`) s oranžovým akcentem, který sedí k elektřině a noci.
- Měření stropu místo magického „dejte 40 %“ — na Ryzenech je `PROCTHROTTLEMAX` jedno z mála nastavení, které opravdu funguje.
- Odpočet s možností zrušit (noční / ruční režim) vs. tichý přepínač po zamknutí.
- Reálný provoz u autora funguje: z `prepni-plan.log` je vidět zamknutí → 2× klid → úsporný režim → ráno návrat na Rovnováhu.

---

## 3. Největší díry v produktu

Tyhle tři věci rozhodují o důvěře. UI je až potom.

### 3.1 Ráno se výkon vrací jen při odemknutí

`nastav-ulohy.ps1` zakládá jen:

- `Uspora-Tyden`
- `Uspora-Vikend`
- `Uspora-Zamknuti`
- `Bezny-Odemknuti`

Jediný návrat do běžného režimu je odemčení relace. Když počítač přes noc přepne a ráno ho nikdo neodemkne (běží dál, RDP, relace se „neodemkla“), zůstane zpomalený celý den.

Chybí úloha **„v X hodin zpět na plný výkon“** — ideálně stejný čas, jako je dnešní tvrdý deadline 7:00.

### 3.2 Deadline 7:00 je zabetonovaný ve skriptu

```powershell
$deadline = if ($ted.Hour -lt 7) { $ted.Date.AddHours(7) } else { $ted.Date.AddDays(1).AddHours(7) }
```

V okně to nikde není. Člověk si nastaví noční přepnutí na 2:00, netuší, že po 7:00 se už nepřepne, a neví, že se v 7:00 ani nevrátí výkon. Čas má být v `config.ini` a na obrazovce Rozvrh.

### 3.3 Klid se pozná jen z CPU (a NVIDIA GPU)

`VytizeniGPU` bez `nvidia-smi` vrací `0`. Práh grafiky v nastavení pak **nic nedělá**, ale pořád se nabízí. V logu je `GPU=0%`.

Klidně přepne uprostřed:

- stahování / seedování (málo CPU),
- hardwarového přehrávání videa na Intel/AMD GPU,
- kopírování na disk,
- kompilace s pauzami mezi joby.

---

## 4. UX

Okno se tváří jako aplikace, ale chová se jako formulář.

### 4.1 Uložení je past

Všechno (přepínače, časy, výsledek měření) žije v paměti, dokud nespustíte **Uložit a nastavit úlohy**. Zavření křížkem i tlačítkem Zavřít změny zahodí. Po měření vyskočí hláška „uložte tlačítkem dole“ — a dole jsou tři tlačítka, z toho dvě nesouvisejí.

Třída `Posuvnik` v `Ui.cs` už existuje, ale v okně **není**. Strop výkonu jde nastavit jen měřením. Když měření selže, zůstane výchozích 50 % a uživatel neví, jak to změnit ručně.

Chybí:

- indikátor neuložených změn,
- dotaz při zavření,
- automatické uložení po úspěšném měření (nebo aspoň zvýrazněné Uložit).

### 4.2 Jazyk nastavení je implementace, ne záměr

| Teď v UI | Co to ve skutečnosti znamená |
|---|---|
| Měření klidu `3` + Interval `300` s | 15 minut souvislého klidu |
| Odpočet ve varovném okně | vteřiny na rozmyšlenou, než se přepne |
| Zakázat uspávání a hibernaci | invazivní zásah do schémat Windows; projeví se až při příštím běhu `prepni-plan.ps1` |

3 × 300 s = **15 minut klidu**. To má být na obrazovce: *„Přepni, až bude počítač 15 minut v klidu.“* Počet vzorků a interval schovat pod Pokročilé.

### 4.3 První spuštění je tiché

Výchozí stav v kódu: noční přepnutí vypnuté, zamknutí vypnuté, TV vypnutá. Otevře se pět karet, nic se neděje, dokud uživatel nezapne přepínače a neuloží. Chybí jedna obrazovka: *co chcete hlídat* (noc / zámek / obojí) a tlačítko Zapnout.

Poznámka: `config.ini` u autora už má vše zapnuté; `Vychozi()` v kódu je konzervativnější. Nový uživatel po instalaci uvidí prázdný, tichý stav.

### 4.4 Stav a záznam je dump pro vývojáře

Surový `powercfg`, názvy úloh, posledních 20 řádků logu. Člověk chce vidět:

- teď **Úsporný / Běžný**,
- další akci: *dnes ve 00:00 čekám na klid*,
- televizi: spárovaná / nedostupná,
- poslední rozhodnutí: *v 01:35 přepnuto, CPU 12 %*.

`lg-tv.log` a `ulohy.log` se v okně neukazují vůbec.

### 4.5 Varovné okno je z jiného programu

Hlavní app: vlastní chrome, karty, oranžová. Odpočet v `prepni-plan.ps1`: standardní `FixedDialog`, jiná šedá, jediné tlačítko „Zrušit (nechat současný režim)“.

Chybí:

- **Odložit o hodinu**,
- **Přepnout hned**,
- stejný vizuál jako hlavní okno.

Když o půlnoci ještě sedíte u počítače, zrušení = tato noc se už nezkusí. Žádný retry.

### 4.6 Dvě stejné karty všední / víkend

Stejný formulář 2×. Dny se můžou překrýt nebo nechat díru (pátek ve všedních odškrtnutý a ve víkendu taky). Místo toho jeden týdenní rozvrh: čas začátku, čas konce, které dny.

### 4.7 Po zavření okna stav nikde není

Aplikace po zavření zmizí. Žádná ikona u hodin, žádný aktuální režim, žádné rychlé přepnutí. To je důsledek architektury bez rezidentního procesu — ale tray by mohl existovat jen jako prohlížeč stavu (čtení aktivního schématu + logu), ne jako hlídací služba.

---

## 5. UI

Vypadá moderně, ale chová se jako starší WinForms.

### 5.1 Rozložení je nakreslené na jeden monitor

- Okno `960×800`, `FormBorderStyle.None`.
- Bez maximalizace, bez resize, bez snapu, bez DPI awareness.
- Absolutní souřadnice `x,y` na kartách.
- Tlačítka min / zavřít natvrdo vpravo nahoře podle `ClientSize` v konstruktoru.
- V `build.cmd` není `/win32icon` — na hlavním panelu generic exe.

Na notebooku 1366×768 / 150 % DPI okno přeteče.

### 5.2 Dva vizuální světy v jednom okně

Vlastní: `Prepinac`, `Tlacitko`, `Cislovac`, `Karta`, `Pole`.  
Systémové a nesladěné: `DateTimePicker`, `CheckedListBox`, `ComboBox` (`Vyber` jen částečně), `MessageBox`.

Na tmavém Windows 11 to často spadne do bílého kalendáře a světlého seznamu dnů. To rozbije dojem víc než chybějící stín.

Odpočet v PowerShellu je třetí vizuální jazyk.

### 5.3 Hierarchie akcí

Zelené primární tlačítko („Uložit“, „Změřit“) bije s oranžovým akcentem (`#FF953D`). Zeleň sem patří jako *stav* (běží, spárováno), ne jako CTA. Primární akce ať je oranžová — jako power LED, který už je v titulku.

Dole tři tlačítka + nahoře křížek + dole Zavřít. Stačí: jedna primární, jedna sekundární, zavření jednou.

### 5.4 Chybí „teď“ jako hrdina obrazovky

První stránka jsou dropdowny schémat. Pro dispečink napájení má být nahoře velký stav:

```
┌─────────────────────────────────────────────┐
│  ● ÚSPORNÝ REŽIM              40 % strop    │
│  Další: zítra 07:00 → běžný režim           │
│  Televize: pohotovost · 192.168.250.23      │
└─────────────────────────────────────────────┘
```

Oranžová tečka v titulku ten jazyk začíná — dál s ním UI nepracuje.

### 5.5 Návrh obrazovky (směr, ne mockup)

Ne další sada stejných karet. Jazyk **dispečinku**: nahoře živý stav, uprostřed čas, dole výjimky.

```
┌ Napájení Manager ──────────────── ● Úsporný ── ☐ ✕ ┐
│ Stav          Rozvrh          Zámek         Televize │
│                                                      │
│  ┌──────────────────────────────────────────────┐    │
│  │  TEĎ  Úsporný režim    strop 40 % · 690 MHz  │    │
│  │  Další akce  07:00  →  Běžný (Rovnováha)     │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  00          08          16          00              │
│  ──────────[======= noc =======]──────────           │
│             00:00                 07:00              │
│  Po–Pá  00:00–07:00     So–Ne  02:00–09:00           │
│                                                      │
│  Přepnout až po 15 min klidu                         │
│  ☑ Odejdu od počítače → taky úspora                  │
│                                                      │
│                     [ Uložit rozvrh ]                │
└──────────────────────────────────────────────────────┘
```

Paleta nechat: pozadí `#18191C`, karta `#26282D`, akcent `#FF953D` jako žhavé vlákno. Zeleň jen na „běží / spárováno“. Segoe UI je tady správně — je to systémový nástroj Windows, ne webová landing page.

Karty všední/víkend sloučit. `CheckedListBox` nahradit řadou mini-tlačítek Po–Ne. Čas nahradit vlastním ovládáním ve stejném stylu jako `Cislovac`.

### 5.6 Přístupnost a klávesnice

`Tlacitko`, `Prepinac`, `Cislovac`, `NavPolozka` jsou holé `Control`. Tab, mezerník, Enter, šipky skoro nefungují. Chybí `AccessibleName`.

V `OnPaint` se pokaždé tvoří nový `Font` (titulek okna, aktivní položka menu) a v `Karta` nový `SolidBrush` — únik GDI při delším otevření okna.

`Cascadia Mono` na stroji bez tohoto písma spadne na fallback a tabulka měření se rozhází. Potřeba fallback (Consolas).

---

## 6. Funkce — návrh podle dopadu

### 6.1 Rychlé výhry

1. **Ruční strop výkonu** — použít už hotový `Posuvnik`; měření jen navrhne hodnotu.
2. **Návrat ráno v nastavitelný čas**, nejen odemknutí.
3. **Deadline v UI** a stejný čas jako ranní návrat.
4. **Neuložené změny** — tečka u Uložit, dotaz při zavření.
5. **Jedna doba klidu** místo počtu × intervalu.
6. **Ikona aplikace** + verze v okně / instalátoru.
7. **Stav GPU** — když není NVIDIA, práh schovat a říct proč.
8. **Oprava vypnutí TV** — v logu je reálná chyba WebSocketu.

### 6.2 Střední vrstva (chování, kterému se dá věřit)

9. **Domovská karta = živý stav**, nastavení až za ní.
10. **Průvodce prvním spuštěním** (3 volby: jen noc / jen zámek / obojí).
11. **Odpočet ve stejném vizuálu** + Odložit / Přepnout hned / Zrušit tuto noc.
12. **Klid i podle sítě a disku** (aspoň bajty/s přes `PerformanceCounter`).
13. **Výjimky procesů** — „když běží `blender.exe` / `code.exe` / `ffmpeg`, nepřepínej“.
14. **Ikona u hodin** — aktuální režim, rychlé Přepnout / Otevřít / Ukončit.
15. **Měření na pozadí** s průběhem 10 % → 90 %, ne zamrzlé `WaitCursor` + `DoEvents`.
16. **Zapnout televizi** (Wake-on-LAN) při návratu do běžného režimu — teď umíte jen zhasnout.
17. **Rotace logů** — `prepni-plan.log` poroste donekonečna.

### 6.3 Větší produktové kroky

18. **Jeden týdenní timeline** místo dvou karet všední/víkend.
19. **Profil notebook vs. desktop** — na baterii agresivnější úspora, na síti dnešní chování. `DontStopIfGoingOnBatteries` už v úloze je, v UI nic.
20. **Ranní „zahřátí“ před budíkem** — 10 minut před návratem zvednout strop, ať odemčení není na 700 MHz.
21. **AMD/Intel GPU** — číst `GPU Engine` v čítačích Windows 11.
22. **Víc TV / HDMI-CEC / Hue** — LG je fajn, ale je to jedna značka na jedné kartě.
23. **Odhad úspory** — hrubé watty z TDP × strop, ať má „40 %“ lidský význam.
24. Lokalizace EN (instalátor už má `en` jazyk, aplikace ne).

---

## 7. Technické dluhy, které UX tiše kazí

### 7.1 Noční úloha vs. odemčení

Úloha na noc může běžet až 8 hodin (`-ExecutionTimeLimit`). Mezitím odemčení vrátí běžný režim. Pokud noční skript ještě čeká na klid, po dalším utichnutí znovu stáhne výkon — i ráno v 6:30, kdy už je člověk u počítače.

### 7.2 Přepínače, které lžou, dokud něco nepoběží

- **`ZakazatSpanek`** mění schémata Windows až v `prepni-plan.ps1`, ne při uložení.
- **Strop procesoru** se zapíše až při přepnutí na úsporu (`Akce Uspora`), ne při uložení. Změříte 40 %, uložíte, ale `powercfg` se změní až v noci.

### 7.3 Odemčení skáče opakovaně

V logu:

```
08:34:10  PREPNUTO na 'Rovnováha' (rezim: Odemknuti)
08:50:23  PREPNUTO na 'Rovnováha' (rezim: Odemknuti)
08:58:35  PREPNUTO na 'Rovnováha' (rezim: Odemknuti)
```

Windows spouští session trigger opakovaně (RDP, screensaver, rychlé zamknutí). Úloha by měla být no-op, když už běžný režim běží.

### 7.4 Televize

Z `prepni-plan.log`:

```
TV: chyba - Exception calling "GetResult" … Objekt WebSocket je pro tuto operaci
v neplatném stavu (CloseReceived). Platné stavy jsou: Open, CloseSent
```

Příkaz `ssap://system/turnOff` televizi zhasne, TV zavře socket, skript to hlásí jako chybu. Obsluha má po `turnOff` očekávat uzavření spojení.

Hledání TV jde jen přes ARP sousedy a port 3001. Televize mimo tabulku sousedů se nenašla, i když IP znáte. Chybí ruční „ověřit spojení“ jako stav v kartě (skript `-Stav` už existuje, UI ho nenabízí).

### 7.5 Ostatní

- Žádný mutex — dvě okna, dvojí zápis úloh.
- `MessageBox` blokuje a vizuálně nesedí; stačí toast ve spodní liště.
- `SpustPS` / `SpustPSSync` nekontrolují exit kód; „uloženo“ může lhát.
- UI vlákno se blokuje při měření (až 10 min timeout) a hledání TV.
- Logy bez rotace a bez horního limitu.
- Duplicitní parser `config.ini` ve čtyřech skriptech + C#.
- Hardcoded GUID výchozích schémat Balanced / Power saver — na některých instalacích Windows 11 schémata chybí nebo mají jiný název.
- Měření: jednovláknová smyčka `Sqrt` neměří reálnou odezvu desktopu (přihlášení, RDP). Pro účel „nespadnout pod 500 MHz“ stačí, ale tabulka působí přesněji, než je.

---

## 8. Doporučené pořadí, kdyby se šlo do kódu

| Pořadí | Změna | Proč |
|---|---|---|
| 1 | Domovská karta se stavem + dalším časem | Aplikace začne dávat smysl na první pohled |
| 2 | Ranní návrat + deadline v nastavení | Opraví reálné chování, ne jen vzhled |
| 3 | Posuvník stropu + neuložené změny | Měření přestane být jediná cesta |
| 4 | Lidská „doba klidu“ | Zmizí nejhorší odborný žargon |
| 5 | Stejný odpočet + Odložit | Noční scénář, kvůli kterému to vzniklo |
| 6 | Ikona, DPI, menší okno / scroll | Půjde to použít na notebooku |
| 7 | Oprava TV WebSocket + stav spárování | Je to v logu jako chyba, i když TV zhasla |

Až potom: tray, výjimky procesů, timeline, síť/disk jako signál klidu.

---

## 9. Shrnutí jednou větou

Nástroj řeší skutečný problém a jádro (Plánovač + čekání na klid + strop CPU) drží; aby z něj byl produkt, musí ukázat **co se děje teď**, **vrátit výkon i bez odemčení**, **nechat lidi nastavit strop ručně** a **přestat mluvit v implementačních jednotkách**.

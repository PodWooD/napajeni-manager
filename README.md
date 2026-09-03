# Napájení Manager

Automatické přepínání režimů napájení ve Windows — ale s ohledem na to, co počítač zrovna dělá.

Windows umí buď uspat, nebo neudělat nic. Když necháte přes noc běžet render, kompilaci nebo stahování,
plánovač je vypne bez ptaní. Napájení Manager místo toho **počká, až bude počítač skutečně nečinný**,
teprve pak sníží výkon — a při odemknutí ho zase vrátí zpátky.

Počítač zůstává zapnutý a dostupný. Žádné uspávání, žádná hibernace. Kdykoliv se k němu přihlásíte,
lokálně i přes vzdálenou plochu, a během vteřiny je zase v plné síle.

## Co to umí

- **Noční přepnutí** — v zadaný čas a dny přepne na úsporné schéma. Zvlášť nastavitelné pro všední dny a víkend.
- **Čekání na klid** — přepne se až ve chvíli, kdy vytížení procesoru i grafiky opakovaně klesne pod zadanou mez.
  Běžící úloha tak přepnutí odsune, dokud neskončí (nejpozději do 7:00).
- **Přepnutí při zamknutí** — odejdete od počítače, po chvíli klidu se sám zpomalí.
- **Návrat při odemknutí** — okamžitě a bez ptaní zpět do běžného režimu.
- **Automatické změření stropu výkonu** — program sám vyzkouší devět úrovní omezení procesoru
  a vybere tu nejúspornější, u které počítač ještě zůstane příjemně ovladatelný.
- **Vypnutí televize LG** — pokud máte televizi webOS jako monitor, uvede se zároveň do pohotovosti.
- **Varovné okno s odpočtem** — než se režim přepne, lze to jedním klikem zrušit.

## Požadavky

| | |
|---|---|
| Systém | Windows 10 nebo 11 |
| Runtime | .NET Framework 4.0 — součást Windows, nic se neinstaluje |
| Oprávnění | Běžný uživatel. Správce není potřeba. |
| Volitelně | `nvidia-smi` pro sledování grafiky (instaluje se s ovladači NVIDIA) |
| Volitelně | Televize LG s webOS na stejné síti |

## Instalace

Stáhněte si `NapajeniManager-1.0-setup.exe` ze [sekce Releases](https://github.com/PodWooD/napajeni-manager/releases)
a spusťte ho. Instalace **nevyžaduje účet správce** — program se nainstaluje do
`%LOCALAPPDATA%\Programs\NapajeniManager`.

Odinstalace probíhá běžnou cestou přes `Nastavení → Aplikace`. Kromě souborů
odstraní i všechny naplánované úlohy, které program vytvořil.

### Sestavení ze zdrojáků

Aplikaci přeloží dvojklik na `build.cmd`. Není potřeba Visual Studio ani SDK,
kompilátor je součástí Windows:

```
build.cmd
```

Instalátor se sestaví pomocí [Inno Setup 6](https://jrsoftware.org/isinfo.php);
výsledek se objeví ve složce `build`:

```
ISCC.exe installer.iss
```

## Použití

Aplikace má pět stránek.

### Režimy napájení

Vyberte, které schéma je „běžné“ a které „úsporné“. Nabídka se plní z `powercfg /list`,
takže lze použít i vlastní schémata.

Tlačítko **Změřit a nastavit automaticky** spustí měření. Program postupně omezí procesor
na 10 až 90 % a u každé úrovně změří skutečný takt a dobu jednoduchého výpočtu. Pak vybere
nejnižší hodnotu, která ještě splňuje obě podmínky — takt nad 500 MHz a zpomalení do trojnásobku.
Trvá to zhruba minutu a původní stav se vrátí.

Příklad výstupu na Ryzenu 9 9950X3D:

```
V úsporném režimu poběží počítač zhruba 2,1× pomaleji (693 MHz),
ale zůstane plně ovladatelný — přihlášení i vzdálený přístup budou svižné.

  Omezení  Takt        Zpomalení
    100 %    5461 MHz  plný výkon   běžný režim
     10 %      87 MHz        8,9×   příliš pomalé
     20 %     113 MHz        7,1×   příliš pomalé
     30 %     385 MHz        2,9×   příliš pomalé
     40 %     693 MHz        2,1×   ← nastaveno
     50 %    1075 MHz        1,5×
     60 %    1540 MHz        1,2×
     70 %    2111 MHz        1,1×
```

Dole na stránce se nastavuje délka odpočtu ve varovném okně a prahy, pod kterými se počítač
považuje za nečinný.

### Noční přepnutí

Čas, dny a chování zvlášť pro pracovní týden a pro víkend. `Kolikrát klid` znamená, kolikrát
po sobě musí být obě vytížení pod prahem, než se opravdu přepne — při zátěži se počitadlo
vynuluje a čeká se dál.

### Zamknutí počítače

Reaguje na zamknutí relace. Před přepnutím se ověří, že jste se mezitím nevrátili.

### Televize

**Vyhledat** projde sousedy na síti a najde zařízení s otevřeným portem 3001.
**Spárovat** se jednorázově potvrdí dálkovým ovladačem; klíč se uloží do `lg-tv-key.txt`.

### Stav a záznam

Aktivní schéma, seznam naplánovaných úloh a posledních několik desítek řádků protokolu.

## Jak to funguje uvnitř

Aplikace sama nic nehlídá a na pozadí neběží. Je to jen nastavovací okno, které zapisuje
`config.ini` a zakládá úlohy v Plánovači úloh Windows. Vlastní práci dělají skripty PowerShellu.

| Soubor | Role |
|---|---|
| `NapajeniManager.cs`, `Ui.cs` | Okno aplikace (WinForms, vlastní kreslené ovládací prvky) |
| `prepni-plan.ps1` | Vlastní přepínání: čekání na klid, odpočet, vypnutí TV, zamknutí |
| `nastav-ulohy.ps1` | Zakládá a ruší úlohy v Plánovači podle `config.ini` |
| `zmer-vykon.ps1` | Změří dopad stropu výkonu a doporučí hodnotu |
| `lg-tv.ps1` | Ovládání televize LG přes SSAP (`wss://<ip>:3001`) |
| `config.ini` | Nastavení. Vytvoří si ho aplikace, do repozitáře nepatří. |
| `installer.iss` | Předpis instalátoru pro Inno Setup |

Úlohy se jmenují `NapajeniManager-*`. Přepnutí při zamknutí a odemknutí využívá
`MSFT_TaskSessionStateChangeTrigger` (stav 7 = zamknutí, 8 = odemknutí).

Strop výkonu se nastavuje přes `PROCTHROTTLEMAX`
(GUID `bc5038f7-23e0-4960-96da-33abaf5935ec`) v podskupině procesoru.

Protokoly se zapisují vedle skriptů: `prepni-plan.log`, `ulohy.log`, `lg-tv.log`.

## Poznámka k procesorům AMD Ryzen

Na Ryzenech se používá CPPC — frekvenci si řídí sám procesor a Windows do ní téměř nemluví.
Většina položek ve schématu napájení proto hlásí `n/a` a nemá žádný účinek. **`PROCTHROTTLEMAX`
je jedno z mála nastavení, které skutečně funguje**, a proto na něm celý úsporný režim stojí.

Právě kvůli tomu má program vestavěné měření místo pevných doporučených hodnot: co znamená
„40 %“ se liší počítač od počítače a jediný poctivý způsob, jak to zjistit, je změřit to.

## Ochrana soukromí

`config.ini` a `lg-tv-key.txt` jsou v `.gitignore`. Obsahují IP adresu vaší televize
a párovací klíč, které do veřejného repozitáře nepatří. Vzorové nastavení s popisem
všech položek najdete v `config.ini.vzor`.

Program nikam nic neodesílá a nepřipojuje se na internet. Jediná síťová komunikace
je volitelné spojení s televizí ve vaší vlastní síti.

## Řešení potíží

**Měření skončí hláškou „Měření nevrátilo výsledek“**
Skript nejspíš neprošel parserem. Soubory `.ps1` musí být uložené jako **UTF-8 s BOM** —
bez něj je Windows PowerShell 5.1 čte jako CP1250, české znaky se rozpadnou a skript spadne.
Ověření:

```powershell
$e = $null
[System.Management.Automation.Language.Parser]::ParseFile('zmer-vykon.ps1', [ref]$null, [ref]$e)
$e
```

**Úlohy se nezaložily**
Zkontrolujte je v `Plánovači úloh` pod názvem `NapajeniManager-*`, případně:

```powershell
Get-ScheduledTask -TaskName 'NapajeniManager-*'
```

**Počítač se v noci nepřepnul**
Nejspíš správně — něco běželo. Důvod je v `prepni-plan.log`, který u každé kontroly
zaznamenává naměřené vytížení.

**Televize se nevypíná**
Klíč mohl vypršet. Smažte `lg-tv-key.txt` a spárujte znovu. Stav ověříte:

```powershell
.\lg-tv.ps1 -Stav
```

**Nefunguje sledování grafiky**
Práh GPU se vyhodnocuje jen s dostupným `nvidia-smi`. Bez něj se počítá s nulou,
takže o přepnutí rozhoduje pouze procesor.

## Licence

MIT — viz [LICENSE](LICENSE).

; Napájení Manager — instalační skript pro Inno Setup 6
; Sestavení:  ISCC.exe installer.iss   (výsledek najdete ve složce build\)

#define NazevApp   "Napájení Manager"
#define VerzeApp   "1.0"
#define AutorApp   "PodWooD"
#define WebApp     "https://github.com/PodWooD/napajeni-manager"
#define ExeApp     "NapajeniManager.exe"

[Setup]
AppId={{C6E432E0-A18B-4B2E-9718-CB57D2749BBB}
AppName={#NazevApp}
AppVersion={#VerzeApp}
AppVerName={#NazevApp} {#VerzeApp}
AppPublisher={#AutorApp}
AppPublisherURL={#WebApp}
AppSupportURL={#WebApp}/issues
AppUpdatesURL={#WebApp}/releases

; Instaluje se do profilu uživatele, takže není potřeba účet správce.
; Program si vedle sebe zapisuje config.ini a protokoly — do Program Files
; by na to běžný uživatel neměl právo.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\NapajeniManager
DefaultGroupName={#NazevApp}
DisableProgramGroupPage=yes
DisableDirPage=auto

LicenseFile=LICENSE
OutputDir=build
OutputBaseFilename=NapajeniManager-{#VerzeApp}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0

UninstallDisplayName={#NazevApp}
UninstallDisplayIcon={app}\{#ExeApp}
CloseApplications=yes
RestartApplications=no

VersionInfoVersion=1.0.0.0
VersionInfoCompany={#AutorApp}
VersionInfoDescription=Instalátor programu {#NazevApp}
VersionInfoCopyright=Copyright (c) 2026 {#AutorApp}

[Languages]
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "NapajeniManager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "prepni-plan.ps1";     DestDir: "{app}"; Flags: ignoreversion
Source: "nastav-ulohy.ps1";    DestDir: "{app}"; Flags: ignoreversion
Source: "zmer-vykon.ps1";      DestDir: "{app}"; Flags: ignoreversion
Source: "lg-tv.ps1";           DestDir: "{app}"; Flags: ignoreversion
Source: "config.ini.vzor";     DestDir: "{app}"; Flags: ignoreversion
Source: "README.md";           DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE";             DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#NazevApp}"; Filename: "{app}\{#ExeApp}"
Name: "{group}\{cm:UninstallProgram,{#NazevApp}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#NazevApp}"; Filename: "{app}\{#ExeApp}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeApp}"; Description: "{cm:LaunchProgram,{#NazevApp}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Bez tohohle by v Plánovači zůstaly úlohy odkazující na smazané skripty.
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\nastav-ulohy.ps1"" -Odebrat"; \
    RunOnceId: "OdebratUlohy"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: files; Name: "{app}\config.ini"
Type: files; Name: "{app}\lg-tv-key.txt"
Type: files; Name: "{app}\*.log"
Type: dirifempty; Name: "{app}"
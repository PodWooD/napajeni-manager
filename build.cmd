@echo off
REM Sestaveni Napajeni Manageru. Staci .NET Framework 4.0, ktery je
REM soucasti Windows - zadne SDK ani Visual Studio neni potreba.

REM Prepnout se do slozky skriptu - jinak by se pri spusteni odjinud
REM nebo z jineho disku nenasly zdrojaky.
cd /d "%~dp0"

set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Nenalezen kompilator: %CSC%
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /out:NapajeniManager.exe ^
    /win32manifest:NapajeniManager.manifest ^
    /reference:System.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    NapajeniManager.cs Ui.cs AssemblyInfo.cs

if errorlevel 1 (
    echo Sestaveni selhalo.
    exit /b 1
)
echo Hotovo: NapajeniManager.exe

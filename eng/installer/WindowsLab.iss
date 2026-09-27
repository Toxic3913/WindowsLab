; WindowsLab 1.2 — Inno Setup 6
; Preferido cuando ISCC.exe está instalado (winget install JRSoftware.InnoSetup)
; eng/publish.ps1 lo invoca tras llenar artifacts\publish\win-x64

#define MyAppName "WindowsLab"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "WindowsLab"
#define MyAppExeName "WindowsLab.exe"
#define MyAppURL "https://github.com/"
#define MyAppSupportURL "file:///D:/WindowsLab/docs/legal/EULA-es.txt"

[Setup]
AppId={{8F3C1A90-6B2E-4D11-9C4A-A1B2C3D4E5F6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppSupportURL}
DefaultDirName={autopf}\WindowsLab
DefaultGroupName=WindowsLab
DisableProgramGroupPage=no
LicenseFile=..\..\docs\legal\EULA-es.txt
InfoBeforeFile=..\..\docs\legal\EULA-en.txt
OutputDir=..\..\artifacts\installer
OutputBaseFilename=WindowsLab-Setup-Inno
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
SetupIconFile=..\..\assets\WindowsLab.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
SetupLogging=yes
; Builds locales sin Authenticode: SmartScreen puede avisar. Firme con signtool cuando tenga certificado.
; SignTool=...

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear icono en el escritorio"; GroupDescription: "Accesos directos:"; Flags: checkedonce
Name: "desktopinfo"; Description: "Recordatorio: DesktopInfo / BGInfo (Sysinternals)"; GroupDescription: "Extras:"; Flags: unchecked

[Files]
Source: "..\..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\docs\legal\EULA-es.txt"; DestDir: "{app}\legal"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}\legal"; Flags: ignoreversion

[Icons]
Name: "{group}\WindowsLab"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\WindowsLab CLI (ayuda)"; Filename: "{app}\windowslab-cli.exe"; Parameters: "--help"
Name: "{group}\Desinstalar WindowsLab"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WindowsLab"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir WindowsLab"; Flags: nowait postinstall skipifsilent
Filename: "https://learn.microsoft.com/sysinternals/downloads/bginfo"; Description: "Abrir página oficial de BGInfo (Sysinternals)"; Flags: nowait postinstall shellexec unchecked; Tasks: desktopinfo

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  MsgBox('WindowsLab 1.2 — auditoría, configuración y apply con backup.' #13#10 +
         'El apply de sistema requiere opt-in (Ajustes / VM de lab).' #13#10 +
         'Si SmartScreen avisa, el instalador aún no tiene firma Authenticode.',
         mbInformation, MB_OK);
end;

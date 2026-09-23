#define MyAppName "MU Ravendral"
#define MyAppVersion "Season 6 Episode 3"
#define MyAppPublisher "Ravendral"
#define MyAppURL "https://ravendral.com"
#define MyAppExeName "MU Client Ravendral.exe"

[Setup]
AppId={{8DBA8AEF-A08C-4C53-8DAC-88545E6D6B33}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\MU Ravendral
DefaultGroupName=MU Ravendral
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=output
OutputBaseFilename=MU-Ravendral-Season6E3-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "..\*"; DestDir: "{app}"; Excludes: ".git\*,.github\*,installer\*,*.lnk,.gitattributes,.gitignore,*Borderless*,main.exe_original,MuEngTest*,MuError*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\MU Ravendral"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\MU Ravendral"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir MU Ravendral"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

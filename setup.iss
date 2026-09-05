; Inno Setup script for LadderToArduino
; ------------------------------------------------------------
; HOW TO USE (on your Windows machine):
;   1. Install .NET 6 SDK:      https://dotnet.microsoft.com/download
;   2. Install Inno Setup:      https://jrsoftware.org/isdl.php  (free)
;   3. Open a terminal in the "LadderToArduino\LadderToArduino" folder and run:
;        dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
;      This creates publish\LadderToArduino.exe
;   4. Right-click this file (setup.iss) -> "Compile" (or open it in the Inno Setup app
;      and press Compile / F9).
;   5. The finished installer appears at: Output\LadderToArduino-Setup.exe
;      Double-click it to install the app like any normal Windows program
;      (Start Menu shortcut, uninstaller, the works).
; ------------------------------------------------------------

#define MyAppName "Ladder to Arduino"
#define MyAppVersion "1.0"
#define MyAppPublisher "LadderToArduino"
#define MyAppExeName "LadderToArduino.exe"

[Setup]
AppId={{7F1B2C3D-9E4A-4B5C-8D6E-1A2B3C4D5E6F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=LadderToArduino-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
; No admin rights required - installs for current user
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Adjust the source path below if your publish output folder is elsewhere
Source: "publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "{#MyAppExeName}"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

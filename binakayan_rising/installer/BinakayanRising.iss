; Source files and version are supplied by the GitHub Actions workflow.
#define AppSource GetEnv("APP_SOURCE_DIR")
#define AppVersion GetEnv("APP_VERSION")

[Setup]
AppId={{9DB5DD93-A52A-4A10-93CD-2933724FEE9C}
AppName=Binakayan Rising
AppVersion={#AppVersion}
AppPublisher=Kylle
DefaultDirName={autopf}\Binakayan Rising
DefaultGroupName=Binakayan Rising
UninstallDisplayIcon={app}\Binakayan Rising.exe
OutputDir=Output
OutputBaseFilename=BinakayanRising-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Binakayan Rising"; Filename: "{app}\Binakayan Rising.exe"
Name: "{autodesktop}\Binakayan Rising"; Filename: "{app}\Binakayan Rising.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\Binakayan Rising.exe"; Description: "Launch Binakayan Rising"; Flags: nowait postinstall skipifsilent

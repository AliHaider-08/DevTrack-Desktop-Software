#define AppName "DevTrack"
#define AppVersion "1.0.0"
#define AppPublisher "Devzone System"
#define PublishDir "..\artifacts\DevTrack-win-x64"

[Setup]
AppId={{901DA087-432B-4C84-BED8-B4DC7AE50229}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\DevTrack
DefaultGroupName=DevTrack
UninstallDisplayIcon={app}\DevTrack.exe
OutputDir=..\artifacts\installer
OutputBaseFilename=DevTrack-Setup-{#AppVersion}-x64
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=admin
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\DevTrack"; Filename: "{app}\DevTrack.exe"
Name: "{autodesktop}\DevTrack"; Filename: "{app}\DevTrack.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\DevTrack.exe"; Description: "Launch DevTrack"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Business records live under LocalAppData\DevTrack\Data, outside the install folder.
; Uninstall deliberately leaves the database untouched.


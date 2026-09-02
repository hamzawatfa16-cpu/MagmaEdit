#ifndef MagmaEditVersion
#define MagmaEditVersion "0.1.0"
#endif

[Setup]
AppId={{9DCE2F83-2A9E-4B2A-9B72-5F8F1B8C2C11}
AppName=MagmaEdit
AppVersion={#MagmaEditVersion}
AppVerName=MagmaEdit {#MagmaEditVersion}
AppPublisher=Hamza Watfa
AppPublisherURL=https://github.com/hamzawatfa16-cpu/MagmaEdit
AppSupportURL=https://github.com/hamzawatfa16-cpu/MagmaEdit
DefaultDirName={localappdata}\Programs\MagmaEdit
DefaultGroupName=MagmaEdit
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=MagmaEdit-{#MagmaEditVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=MagmaEdit

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\MagmaEdit"; Filename: "{app}\MagmaEdit.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\MagmaEdit"; Filename: "{app}\MagmaEdit.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\MagmaEdit.exe"; Description: "Launch MagmaEdit"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

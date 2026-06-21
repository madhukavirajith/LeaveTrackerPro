; Inno Setup script for LeaveTrackerPro
; Requires Inno Setup (https://jrsoftware.org/isinfo.php)
[Setup]
AppName=LeaveTrackerPro
AppVersion=1.0
DefaultDirName={pf}\LeaveTrackerPro
DefaultGroupName=LeaveTrackerPro
DisableProgramGroupPage=yes
OutputDir={#SourcePath}\publish
OutputBaseFilename=LeaveTrackerPro_Installer
Compression=lzma2
SolidCompression=yes

[Files]
; Copy all published files from the publish folder
Source: "{#SourcePath}\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LeaveTrackerPro"; Filename: "{app}\LeaveTrackerPro.exe"
Name: "{commondesktop}\LeaveTrackerPro"; Filename: "{app}\LeaveTrackerPro.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: iscustom

[Run]
Filename: "{app}\LeaveTrackerPro.exe"; Description: "Launch LeaveTrackerPro"; Flags: nowait postinstall skipifsilent

; NOTE: Update {#SourcePath} before compiling or compile from the project folder.

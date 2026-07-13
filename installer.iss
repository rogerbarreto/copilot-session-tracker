#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{A7C4E2B1-9F3D-4A6E-B8C1-2D5E7F9A3B64}
AppName=Copilot Session Tracker
AppVersion={#MyAppVersion}
AppPublisher=Community
AppPublisherURL=https://github.com/rogerbarreto/copilot-session-tracker
DefaultDirName={userappdata}\CopilotSessionTracker
DefaultGroupName=Copilot Session Tracker
DisableProgramGroupPage=yes
OutputDir=installer-output
OutputBaseFilename=CopilotSessionTracker-Setup
SetupIconFile=src\CopilotSessionTracker\Assets\AppIcon.ico
UninstallDisplayIcon={app}\CopilotSessionTracker.exe
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=yes

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Copilot Session Tracker"; Filename: "{app}\CopilotSessionTracker.exe"; IconFilename: "{app}\CopilotSessionTracker.exe"; AppUserModelID: "CopilotSessionTracker"
Name: "{userdesktop}\Copilot Session Tracker"; Filename: "{app}\CopilotSessionTracker.exe"; Tasks: desktopicon; AppUserModelID: "CopilotSessionTracker"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\CopilotSessionTracker.exe"; Description: "Launch Copilot Session Tracker"; Flags: nowait postinstall skipifsilent
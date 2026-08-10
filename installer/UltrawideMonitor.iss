#define AppName "Ultrawide Monitor"
#define AppVersion "0.1.0"
#define AppPublisher "Gil Breysse (RED)"
#define AppExeName "UltrawideMonitor.exe"
#define AgentExeName "UltrawideMonitor.ElevatedAgent.exe"
#define AgentTaskName "Ultrawide Monitor - Agent administrateur"

[Setup]
AppId={{7D3E5B67-6D0E-4B21-9FA9-0A7A1D0B7A01}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/ImRedTV
AppSupportURL=https://github.com/ImRedTV/Ultrawide-Monitor/issues
DefaultDirName={autopf}\Ultrawide Monitor
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; The startup entry intentionally targets the installing user's HKCU profile.
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=UltrawideMonitor-Setup-x64
; Use the transparent brand icon shared with the application and tray icon.
SetupIconFile=..\src\UltrawideToys.App\assets\ultrawidemonitor.ico
UninstallDisplayIcon={app}\{#AppExeName}
LicenseFile=LICENSE-FR.txt
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\UltrawideToys.SingleInstance
Uninstallable=yes
CloseApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "startup"; Description: "Démarrer Ultrawide Monitor avec Windows"; GroupDescription: "Options de démarrage :"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\publish\{#AgentExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Ultrawide Monitor"; Filename: "{app}\{#AppExeName}"; Parameters: "--settings"
Name: "{autodesktop}\Ultrawide Monitor"; Filename: "{app}\{#AppExeName}"; Parameters: "--settings"; Tasks: startup

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "UltrawideMonitor"; ValueData: "{app}\{#AppExeName} --startup"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--settings"; Description: "Ouvrir les réglages d’Ultrawide Monitor"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""{#AgentTaskName}"" /F"; Flags: runhidden; RunOnceId: "DeleteElevatedAgentTask"

[Code]
var
  KeepSettings: Boolean;

function AskKeepSettings: Boolean;
begin
  Result := MsgBox('Souhaitez-vous conserver vos dispositions et préférences Ultrawide Monitor pour une prochaine installation ?', mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(Step: TUninstallStep);
begin
  if Step = usUninstall then
    KeepSettings := AskKeepSettings;
  if (Step = usPostUninstall) and (not KeepSettings) then begin
    DelTree(ExpandConstant('{localappdata}\UltrawideMonitor'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\UltrawideToys'), True, True, True);
  end;
end;

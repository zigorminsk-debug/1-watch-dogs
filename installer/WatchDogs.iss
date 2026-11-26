; ============================================================
; Watch Dogs — Inno Setup script (UTF-8 with BOM)
; Builds setup.exe: GUI + monitoring Windows service + full uninstall.
; ============================================================

#define MyAppName "Watch Dogs"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Watch Dogs"
#define MyAppExeName "WatchDogs.exe"
#define MyServiceName "WatchDogsMonitor"
#define MyServiceExe "WatchDogs.Service.exe"

[Setup]
AppId={{7D3A1FF1-5E2B-4C6A-9F8E-4A1B2C3D4E5F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\dist
OutputBaseFilename=WatchDogs-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoCompanyText={#MyAppPublisher}
VersionInfoFileDescription=Watch Dogs Setup
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "installservice"; Description: "Install and start monitoring service {#MyServiceName} (auto start with Windows)"; GroupDescription: "Monitoring service:"; Flags: checkedonce

[Files]
; GUI
Source: "..\src\WatchDogs.Gui\bin\Release\net48\WatchDogs.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\WatchDogs.Gui\bin\Release\net48\WatchDogs.exe.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Service + shared core
Source: "..\src\WatchDogs.Service\bin\Release\net48\WatchDogs.Service.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\WatchDogs.Service\bin\Release\net48\WatchDogs.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\WatchDogs.Service\bin\Release\net48\WatchDogs.Service.exe.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Default config (created on first run if missing)
Source: "..\installer\default-config.json"; DestDir: "{commonappdata}\WatchDogs"; DestName: "config.json"; Flags: onlyifdoesntexist ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyServiceExe}"; Parameters: "--install"; StatusMsg: "Installing monitoring service..."; Tasks: installservice; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyServiceExe}"; Parameters: "--start"; StatusMsg: "Starting monitoring service..."; Tasks: installservice; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyServiceExe}"; Parameters: "--stop"; RunOnceId: "StopMonitor"; Flags: runhidden waituntilterminated skipifdoesntexist
Filename: "{app}\{#MyServiceExe}"; Parameters: "--uninstall"; RunOnceId: "UninstallMonitor"; Flags: runhidden waituntilterminated skipifdoesntexist
Filename: "sc.exe"; Parameters: "stop {#MyServiceName}"; RunOnceId: "ScStop"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete {#MyServiceName}"; RunOnceId: "ScDelete"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{commonappdata}\WatchDogs"
Type: files; Name: "{app}\*.log"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill', '/IM {#MyAppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill', '/IM {#MyServiceExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  Exec('taskkill', '/IM {#MyAppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/IM {#MyServiceExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Exec('sc.exe', 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('sc.exe', 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

; PES3-Disc Windows installer (Inno Setup 6)
; Builds with: powershell -File Build-Installer.ps1

#define MyAppName "PES3-Disc"
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "PES3-Disc"
#define MyAppURL "https://github.com/SlushFlow/PES3-Disc"
#define MyAppExeName "PES3-Disc.exe"
#define DotNet8Url "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
#define DotNet10Url "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"

[Setup]
AppId={{A7F3B2C1-9E4D-4F8A-B1C2-2030A5D15C3D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=PES3-Disc-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start PES3-Disc when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "..\dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Build dist\ first with Build-Installer.ps1

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DotNet8Installer: string;
  DotNet10Installer: string;

function DotNetInstallOk(ResultCode: Integer): Boolean;
begin
  { 0 = success, 1638 = already installed, 3010 = success restart needed }
  Result := (ResultCode = 0) or (ResultCode = 1638) or (ResultCode = 3010);
end;

function DownloadRuntime(const Url, FileName: string; var ErrorCode: Integer): Boolean;
begin
  WizardForm.StatusLabel.Caption := 'Downloading ' + FileName + '...';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  try
    Result := DownloadTemporaryFile(Url, FileName, '', @ErrorCode);
  finally
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;
end;

function InstallRuntime(const InstallerPath, RuntimeName: string): string;
var
  ResultCode: Integer;
begin
  Result := '';
  WizardForm.StatusLabel.Caption := 'Installing ' + RuntimeName + '...';
  if not Exec(InstallerPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Could not run ' + RuntimeName + ' installer.';
    Exit;
  end;
  if not DotNetInstallOk(ResultCode) then
    Result := RuntimeName + ' setup failed (exit code ' + IntToStr(ResultCode) + ').';
end;

function PrepareToInstall(var NeedsRestart: Boolean): string;
var
  ErrorCode: Integer;
begin
  NeedsRestart := False;
  Result := '';

  WizardForm.StatusLabel.Caption := 'Downloading .NET 8 Desktop Runtime...';
  if not DownloadRuntime('{#DotNet8Url}', 'dotnet8-desktop-runtime.exe', ErrorCode) then
  begin
    Result := 'Failed to download .NET 8 Desktop Runtime. Error: ' + IntToStr(ErrorCode);
    Exit;
  end;
  DotNet8Installer := ExpandConstant('{tmp}\dotnet8-desktop-runtime.exe');

  WizardForm.StatusLabel.Caption := 'Downloading .NET 10 Desktop Runtime...';
  if not DownloadRuntime('{#DotNet10Url}', 'dotnet10-desktop-runtime.exe', ErrorCode) then
  begin
    Result := 'Failed to download .NET 10 Desktop Runtime. Error: ' + IntToStr(ErrorCode);
    Exit;
  end;
  DotNet10Installer := ExpandConstant('{tmp}\dotnet10-desktop-runtime.exe');

  Result := InstallRuntime(DotNet8Installer, '.NET 8 Desktop Runtime');
  if Result <> '' then Exit;

  Result := InstallRuntime(DotNet10Installer, '.NET 10 Desktop Runtime');
  if Result <> '' then Exit;

  WizardForm.StatusLabel.Caption := 'Installing PES3-Disc files...';
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

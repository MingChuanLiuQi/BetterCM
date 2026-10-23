#ifdef TestBuild
  #define PayloadRoot "..\artifacts\diagnostics\win-x64"
#else
  #define PayloadRoot "..\artifacts\publish\win-x64"
#endif
#define AppVersion RemoveFileExt(GetFileVersion(PayloadRoot + "\BetterCM.exe"))
#ifdef TestBuild
  #define InstallId "{{7E8D5579-A731-4323-8AD8-61AA89407AC2}"
  #define ProductName "BetterCM QA"
  #define StartupName "BetterCM.QA"
#else
  #define InstallId "{{3497A457-D1C1-4D4C-A10A-64D7B32EC101}"
  #define ProductName "BetterCM"
  #define StartupName "BetterCM"
#endif
[Setup]
AppId={#InstallId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=BetterCM
DefaultDirName={localappdata}\Programs\{#ProductName}
DefaultGroupName={#ProductName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
#ifdef TestBuild
OutputDir=..\artifacts\qa
OutputBaseFilename=BetterCM-{#AppVersion}-QA-Setup-x64
#else
OutputDir=..\artifacts\releases\{#AppVersion}
OutputBaseFilename=BetterCM-{#AppVersion}-Setup-x64
#endif
SetupIconFile=..\Assets\BetterCM.ico
UninstallDisplayIcon={app}\BetterCM.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
SetupLogging=yes
SetupMutex=Local\BetterCM.Setup
VersionInfoVersion={#AppVersion}.0
VersionInfoCopyright=Copyright (c) 2026 BetterCM contributors
#ifdef SignedBuild
SignTool=BetterCMSign
SignedUninstaller=yes
#endif
[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"
[Tasks]
Name: "autostart"; Description: "登录 Windows 后自动启动 BetterCM"; GroupDescription: "启动选项："; Flags: unchecked
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked
[Files]
Source: "{#PayloadRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\BetterCM 设置"; Filename: "{app}\BetterCM.exe"
Name: "{autodesktop}\BetterCM"; Filename: "{app}\BetterCM.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#StartupName}"; ValueData: """{app}\BetterCM.exe"" --background"; Tasks: autostart; Flags: uninsdeletevalue
[Run]
Filename: "{app}\BetterCM.exe"; Description: "启动 BetterCM 并打开设置"; Flags: nowait postinstall skipifsilent
[UninstallRun]
#ifndef TestBuild
Filename: "{app}\BetterCM.exe"; Parameters: "--shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "CloseBetterCM"
#endif
[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode, Attempt: Integer;
begin
  Result := '';
#ifndef TestBuild
  if FileExists(ExpandConstant('{app}\BetterCM.exe')) then begin
    if not Exec(ExpandConstant('{app}\BetterCM.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
      Result := '无法请求 BetterCM 退出。请从托盘退出后重试。';
      Exit;
    end;
    if ResultCode <> 0 then begin
      Result := 'BetterCM 尚未完成退出。请等待系统文件操作结束，再从托盘退出后重试。';
      Exit;
    end;
    for Attempt := 1 to 100 do begin
      if not CheckForMutexes('Local\BetterCM.App,Local\BetterCM.Installation.InUse') then Break;
      Sleep(100);
    end;
    if CheckForMutexes('Local\BetterCM.App,Local\BetterCM.Installation.InUse') then
      Result := 'BetterCM 尚在退出，或其他数据目录的实例仍在运行。请从各实例的托盘退出后重试。';
  end;
#endif
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#StartupName}');
  end;
end;


; 验证码剪贴板安装程序（Inno Setup 6）。
; 由 scripts\Build-Installer.ps1 调用 ISCC 编译，并通过 /D 传入下列常量：
;   AppVersion        包版本，如 1.0.3.0
;   AppMsix           应用 MSIX 文件名
;   StageDir          已签名 MSIX、运行时和证书所在目录
;   OutputDir         exe 输出目录
;   AppDir            应用项目目录（src\OtpClipboard）
;   PackageFamilyName 包系列名，用于安装完成后启动应用
;
; 应用必须以 MSIX 包身份运行（读取通知需要），所以这里不往 Program Files 拷文件：
; 安装程序负责信任签名证书（管理员），再以当前用户身份执行 Install.ps1 安装 MSIX。
; 卸载走 Windows“设置 → 应用”里的 MSIX 条目，因此本安装程序不生成卸载程序。

#ifndef AppVersion
  #error 请通过 scripts\Build-Installer.ps1 构建
#endif

#define AppName "验证码剪贴板"
#define WorkDir "{commonappdata}\OtpClipboardSetup"

[Setup]
AppId={{B11E1D98-24B3-4D84-AE8C-5CFD2E423AC2}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=AppPublisher
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} 安装程序
CreateAppDir=no
Uninstallable=no
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
SetupIconFile={#AppDir}\Assets\AppIcon.ico
InfoBeforeFile=安装说明.txt
OutputDir={#OutputDir}
OutputBaseFilename=OtpClipboard-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
CloseApplications=no

[Languages]
Name: "chs"; MessagesFile: "ChineseSimplified.isl"

[Messages]
chs.ReadyLabel1=安装程序已准备好在你的电脑上安装 [name]。
chs.FinishedLabelNoIcons=[name] 已安装完成，可在开始菜单搜索“[name]”打开。首次使用请点击“授权通知访问”并在系统提示中选择允许。

[Dirs]
Name: "{#WorkDir}"; Flags: deleteafterinstall

[Files]
Source: "{#StageDir}\{#AppMsix}"; DestDir: "{#WorkDir}"; Flags: deleteafterinstall
Source: "{#StageDir}\Microsoft.WindowsAppRuntime.2.msix"; DestDir: "{#WorkDir}"; Flags: deleteafterinstall
Source: "{#StageDir}\OtpClipboard-Dev.cer"; DestDir: "{#WorkDir}"; Flags: deleteafterinstall
Source: "Install.ps1"; DestDir: "{#WorkDir}"; Flags: deleteafterinstall

[Run]
Filename: "{win}\explorer.exe"; Parameters: "shell:AppsFolder\{#PackageFamilyName}!App"; \
  Description: "运行 {#AppName}"; Flags: postinstall nowait skipifsilent runasoriginaluser; Check: InstallSucceeded

[Code]
var
  Succeeded: Boolean;

function InstallSucceeded: Boolean;
begin
  Result := Succeeded;
end;

procedure SetStatus(const Text: String);
begin
  WizardForm.StatusLabel.Caption := Text;
  WizardForm.FilenameLabel.Caption := '';
  WizardForm.ProgressGauge.Style := npbstMarquee;
end;

function ReadErrorFile(const Path: String): String;
var
  Lines: TArrayOfString;
  I: Integer;
begin
  Result := '';
  if LoadStringsFromFile(Path, Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
      Result := Result + Lines[I] + #13#10;
  Result := Trim(Result);
end;

procedure Fail(const Message: String);
begin
  Succeeded := False;
  SuppressibleMsgBox(Message, mbCriticalError, MB_OK, IDOK);
  WizardForm.FinishedLabel.Caption := '{#AppName} 没有安装成功。' + #13#10#13#10 + Message;
end;

procedure InstallPackage;
var
  WorkDir, PowerShell, ErrorFile, Message: String;
  ResultCode: Integer;
begin
  WorkDir := ExpandConstant('{#WorkDir}');
  PowerShell := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  ErrorFile := WorkDir + '\install-error.txt';
  DeleteFile(ErrorFile);

  // MSIX 只接受受信任证书签名的包；写入“本地计算机 → 受信任人”需要管理员权限，安装程序本身已提权。
  SetStatus('正在信任签名证书…');
  if not Exec(ExpandConstant('{sys}\certutil.exe'),
      '-f -addstore TrustedPeople "' + WorkDir + '\OtpClipboard-Dev.cer"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    Fail('导入签名证书失败（certutil 退出码 ' + IntToStr(ResultCode) + '）。');
    exit;
  end;

  // MSIX 按用户安装，必须以发起安装的用户身份执行，而不是提权后的管理员身份。
  SetStatus('正在安装 {#AppName}…');
  if not ExecAsOriginalUser(PowerShell,
      '-NoProfile -ExecutionPolicy Bypass -File "' + WorkDir + '\Install.ps1" -NoLaunch -ErrorFile "' + ErrorFile + '"',
      WorkDir, SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    Message := ReadErrorFile(ErrorFile);
    if Message = '' then
      Message := '安装脚本退出码 ' + IntToStr(ResultCode) + '。';
    Fail(Message);
    exit;
  end;

  Succeeded := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallPackage;
end;

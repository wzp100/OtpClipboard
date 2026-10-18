param(
    # 安装后不自动启动应用。
    [switch]$NoLaunch,
    # 失败时把错误信息写入此文件（UTF-8），供安装程序 exe 显示。
    [string]$ErrorFile
)

$ErrorActionPreference = 'Stop'
trap {
    if ($ErrorFile) { [IO.File]::WriteAllText($ErrorFile, $_.Exception.Message, [Text.UTF8Encoding]::new($true)) }
    break
}
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$packageName = 'B11E1D98-24B3-4D84-AE8C-5CFD2E423AC2'
$runtimeName = 'Microsoft.WindowsAppRuntime.2'
$here = [System.IO.Path]::GetFullPath($PSScriptRoot)

function Write-Step([string]$text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# 从 MSIX 内的 AppxManifest.xml 读出包身份和 Windows App Runtime 依赖。
function Read-PackageManifest([string]$msixPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($msixPath)
    try {
        $reader = New-Object System.IO.StreamReader($zip.GetEntry('AppxManifest.xml').Open())
        try { [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

$appMsix = Get-ChildItem -LiteralPath $here -Filter 'OtpClipboard_*.msix' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$certFile = Join-Path $here 'OtpClipboard-Dev.cer'
$runtimeMsix = Join-Path $here "$runtimeName.msix"

if (-not $appMsix) { throw "安装目录里找不到 OtpClipboard_*.msix：$here" }
if (-not (Test-Path -LiteralPath $certFile)) { throw "安装目录里找不到证书：$certFile" }

$manifest = Read-PackageManifest $appMsix.FullName
$version = [version]$manifest.Package.Identity.Version
$arch = $manifest.Package.Identity.ProcessorArchitecture
Write-Host "验证码剪贴板 $version（$arch）安装程序"

# 1. 信任签名证书：Windows 只安装受信任证书签名的 MSIX；写入“本地计算机”证书库需要管理员权限。
Write-Step '检查签名证书'
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certFile)
$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $cert.Thumbprint
if ($trusted) {
    Write-Host "证书已受信任：$($cert.Subject)"
}
else {
    Write-Host ('需要把证书 {0}（指纹 {1}）导入“本地计算机 → 受信任人”。' -f $cert.Subject, $cert.Thumbprint)
    Write-Host '接下来会弹出管理员权限确认，请选择“是”。'
    $importCommand = "Import-Certificate -FilePath '$($certFile.Replace("'", "''"))' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"
    if (Test-Admin) {
        Invoke-Expression $importCommand
    }
    else {
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes("`$ErrorActionPreference='Stop'; $importCommand"))
        try {
            $elevated = Start-Process -FilePath 'powershell.exe' -Verb RunAs -Wait -PassThru -WindowStyle Hidden `
                -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded)
        }
        catch {
            throw '未获得管理员权限，证书没有导入，安装已取消。'
        }
        if ($elevated.ExitCode -ne 0) { throw "导入证书失败，退出码：$($elevated.ExitCode)" }
    }
    if (-not (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $cert.Thumbprint)) {
        throw '证书导入后仍未在“受信任人”中找到，安装已取消。'
    }
    Write-Host '证书已导入。'
}

# 2. Windows App Runtime：系统已有满足版本的框架包就不重复安装。
Write-Step '检查 Windows App Runtime'
$dependency = $manifest.Package.Dependencies.PackageDependency | Where-Object Name -eq $runtimeName | Select-Object -First 1
$dependencyPaths = @()
if ($dependency) {
    $minVersion = [version]$dependency.MinVersion
    $installedRuntime = Get-AppxPackage -Name $runtimeName |
        Where-Object { $_.Architecture.ToString() -ieq $arch -and [version]$_.Version -ge $minVersion } |
        Select-Object -First 1
    if ($installedRuntime) {
        Write-Host "已安装 $runtimeName $($installedRuntime.Version)，满足最低要求 $minVersion。"
    }
    elseif (Test-Path -LiteralPath $runtimeMsix) {
        Write-Host "未找到 $runtimeName $minVersion 或更高版本，将一并安装附带的运行时。"
        $dependencyPaths = @($runtimeMsix)
    }
    else {
        throw "缺少 $runtimeName $minVersion（$arch），且安装目录里没有 $runtimeName.msix。"
    }
}

# 3. 处理已安装的版本。
Write-Step '检查已安装的版本'
$existing = Get-AppxPackage -Name $packageName
if ($existing) {
    if ($existing.IsDevelopmentMode) {
        # dotnet run / Visual Studio 注册的开发版与正式包冲突，先移除；开发模式下可保留处理记录等本地数据。
        Write-Host "发现开发版注册（$($existing.Version)），先移除它并保留本地数据。"
        Get-Process -Name 'OtpClipboard' -ErrorAction SilentlyContinue | Where-Object {
            try { $_.Path -and $_.Path.StartsWith($existing.InstallLocation, [StringComparison]::OrdinalIgnoreCase) } catch { $false }
        } | Stop-Process -Force
        Remove-AppxPackage -Package $existing.PackageFullName -PreserveApplicationData
    }
    elseif ([version]$existing.Version -eq $version) {
        Write-Host "已安装相同版本 $version，无需重复安装。"
    }
    elseif ([version]$existing.Version -gt $version) {
        throw ('已安装更新的版本 {0}，不会降级到 {1}。如确需降级，请先运行“卸载.cmd”。' -f $existing.Version, $version)
    }
    else {
        Write-Host "将从 $($existing.Version) 升级到 $version，本地数据会保留。"
    }
}
else {
    Write-Host '未安装过，执行全新安装。'
}

# 4. 安装 / 升级（-ForceApplicationShutdown 会关闭正在运行的旧版本）。
$current = Get-AppxPackage -Name $packageName
if (-not $current -or [version]$current.Version -ne $version) {
    Write-Step "安装 $($appMsix.Name)"
    $addArgs = @{ Path = $appMsix.FullName; ForceApplicationShutdown = $true }
    if ($dependencyPaths.Count -gt 0) { $addArgs.DependencyPath = $dependencyPaths }
    Add-AppxPackage @addArgs
    $current = Get-AppxPackage -Name $packageName
    if (-not $current -or [version]$current.Version -ne $version) { throw '安装命令已执行，但没有找到安装后的应用。' }
}

Write-Host ("`n" + ('安装完成：验证码剪贴板 {0}。可在开始菜单搜索“验证码剪贴板”打开。' -f $current.Version)) -ForegroundColor Green
Write-Host '首次使用请点击“授权通知访问”并在系统提示中选择允许。'

if (-not $NoLaunch) {
    Start-Process "shell:AppsFolder\$($current.PackageFamilyName)!App"
}

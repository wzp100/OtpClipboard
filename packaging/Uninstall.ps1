param(
    # 同时从“本地计算机 → 受信任人”删除测试签名证书（需要管理员权限）。
    [switch]$RemoveCertificate
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$packageName = 'B11E1D98-24B3-4D84-AE8C-5CFD2E423AC2'
$here = [System.IO.Path]::GetFullPath($PSScriptRoot)

$existing = Get-AppxPackage -Name $packageName
if ($existing) {
    Write-Host "卸载验证码剪贴板 $($existing.Version)…"
    Get-Process -Name 'OtpClipboard' -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and $_.Path.StartsWith($existing.InstallLocation, [StringComparison]::OrdinalIgnoreCase) } catch { $false }
    } | Stop-Process -Force
    Remove-AppxPackage -Package $existing.PackageFullName
    Write-Host '已卸载。处理记录等本地数据随应用一起删除。' -ForegroundColor Green
}
else {
    Write-Host '当前用户没有安装验证码剪贴板。'
}

# Windows App Runtime 是共享框架包，其他应用也可能依赖它，这里不卸载。

if ($RemoveCertificate) {
    $certFile = Join-Path $here 'OtpClipboard-Dev.cer'
    if (-not (Test-Path -LiteralPath $certFile)) { throw "找不到证书文件：$certFile" }
    $thumbprint = (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certFile)).Thumbprint
    if (-not (Test-Path "Cert:\LocalMachine\TrustedPeople\$thumbprint")) {
        Write-Host '证书不在“受信任人”中，无需删除。'
        return
    }
    Write-Host '删除测试证书需要管理员权限，请在弹出的确认中选择“是”。'
    $command = "`$ErrorActionPreference='Stop'; Remove-Item -LiteralPath 'Cert:\LocalMachine\TrustedPeople\$thumbprint'"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    try {
        $elevated = Start-Process -FilePath 'powershell.exe' -Verb RunAs -Wait -PassThru -WindowStyle Hidden `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded)
    }
    catch {
        throw '未获得管理员权限，证书没有删除。'
    }
    if ($elevated.ExitCode -ne 0) { throw "删除证书失败，退出码：$($elevated.ExitCode)" }
    Write-Host '测试证书已删除。' -ForegroundColor Green
}

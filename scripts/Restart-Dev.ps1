param()

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

# 关闭本仓库构建出的旧开发实例，增量构建并以开发包身份重新启动应用。
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\OtpClipboard'))
$projectFile = Join-Path $projectRoot 'OtpClipboard.csproj'
$buildDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'bin')) + [System.IO.Path]::DirectorySeparatorChar
$layoutRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\.local\development-layout'))
$layoutDirectory = $layoutRoot + [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "找不到项目文件：$projectFile"
}

# 只关闭这个项目构建目录里的旧开发实例，不影响其他同名程序。
$stoppedIds = @()
Get-Process -Name 'OtpClipboard' -ErrorAction SilentlyContinue | ForEach-Object {
    $appProcess = $_
    try {
        $exePath = $appProcess.Path
        if ($exePath -and (
                $exePath.StartsWith($buildDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
                $exePath.StartsWith($layoutDirectory, [System.StringComparison]::OrdinalIgnoreCase))) {
            Write-Host "关闭旧开发实例：PID $($appProcess.Id)"
            Stop-Process -Id $appProcess.Id
            $stoppedIds += $appProcess.Id
            Wait-Process -Id $appProcess.Id -Timeout 5 -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Warning "无法关闭 PID $($appProcess.Id)：$($_.Exception.Message)"
    }
}

# 刚关闭的实例可能仍在退出，此时读不到 Path；按 PID 排除，避免误判为其他安装。
$otherInstances = Get-Process -Name 'OtpClipboard' -ErrorAction SilentlyContinue | Where-Object {
    if ($stoppedIds -contains $_.Id) { return $false }
    try {
        -not $_.Path -or (
            -not $_.Path.StartsWith($buildDirectory, [System.StringComparison]::OrdinalIgnoreCase) -and
            -not $_.Path.StartsWith($layoutDirectory, [System.StringComparison]::OrdinalIgnoreCase)
        )
    }
    catch { $true }
}
if ($otherInstances) {
    throw '检测到另一处安装的“验证码剪贴板”正在运行。请先手动关闭它，再启动此开发实例。'
}

Write-Host '增量构建打包式 Debug 版本…'
& (Join-Path $PSScriptRoot 'Prepare-DevLayout.ps1') -Configuration Debug
if (-not $layoutRoot -or -not (Test-Path -LiteralPath (Join-Path $layoutRoot 'OtpClipboard.exe'))) {
    throw '开发构建布局准备失败。'
}

Write-Host '启动开发实例…'
$startedAt = Get-Date
$launchLogDirectory = Join-Path ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) '.local'
New-Item -ItemType Directory -Force -Path $launchLogDirectory | Out-Null
$launchLog = Join-Path $launchLogDirectory ('launch-' + $startedAt.ToString('yyyyMMdd-HHmmss') + '.log')
$launchErrorLog = $launchLog + '.error.log'
$processArchitecture = if ([Environment]::Is64BitProcess) { 'x64' } else { 'x86' }
$nugetPackages = $env:NUGET_PACKAGES
if (-not $nugetPackages) {
    $nugetPackages = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages'
}
$winappRoot = Join-Path $nugetPackages 'microsoft.windows.sdk.buildtools.winapp\0.7.0\tools'
$winapp = Join-Path (Join-Path $winappRoot ('win-' + $processArchitecture)) 'winapp.exe'
if (-not (Test-Path -LiteralPath $winapp)) {
    throw "找不到当前架构的开发启动器：$winapp"
}
$runner = Start-Process -FilePath $winapp `
    -ArgumentList @(
        'run', ('"' + $layoutRoot + '"'),
        '--manifest', ('"' + (Join-Path $layoutRoot 'AppxManifest.xml') + '"'),
        '--detach'
    ) `
    -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $launchLog -RedirectStandardError $launchErrorLog

$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    $instance = Get-Process -Name 'OtpClipboard' -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.StartTime -ge $startedAt -and $_.Path -and
                ($_.Path.StartsWith($buildDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
                 $_.Path.StartsWith($layoutDirectory, [System.StringComparison]::OrdinalIgnoreCase))
        }
        catch { $false }
    } | Select-Object -First 1

    if ($instance) {
        Start-Sleep -Seconds 2
        $instance.Refresh()
        if ($instance.HasExited) {
            throw "后台进程启动后立即退出：PID $($instance.Id)"
        }
        Write-Host "开发后台已启动：PID $($instance.Id)"
        $launcher = Join-Path $layoutRoot 'OtpClipboard.exe'
        $uiProcess = Start-Process -FilePath $launcher -ArgumentList '--ui' -PassThru
        $windowDeadline = (Get-Date).AddSeconds(15)
        while ((Get-Date) -lt $windowDeadline) {
            $uiProcess.Refresh()
            if ($uiProcess.HasExited) {
                throw "UI 进程启动后立即退出：PID $($uiProcess.Id)"
            }
            if ($uiProcess.MainWindowHandle -ne [IntPtr]::Zero) {
                Write-Host "开发界面已启动：PID $($uiProcess.Id)"
                return
            }
            Start-Sleep -Milliseconds 250
        }
        throw "后台已启动，但 15 秒内没有检测到开发界面：PID $($uiProcess.Id)"
    }

    $runner.Refresh()
    if ($runner.HasExited) {
        $diagnostics = @(
            Get-Content -LiteralPath $launchLog -Encoding utf8 -Tail 20
            Get-Content -LiteralPath $launchErrorLog -Encoding utf8 -Tail 20
        ) -join "`n"
        if ($diagnostics -match ' launched \(PID:') {
            Start-Sleep -Milliseconds 500
            continue
        }
        throw "启动器提前退出，退出码：$($runner.ExitCode)`n$diagnostics`n完整日志：$launchLog"
    }
    Start-Sleep -Milliseconds 250
}

throw '30 秒内未检测到应用窗口，请检查 Windows 应用部署日志。'

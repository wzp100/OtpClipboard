param()

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

# 关闭本仓库构建出的旧开发实例，增量构建并以开发包身份重新启动应用。
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\OtpClipboard'))
$projectFile = Join-Path $projectRoot 'OtpClipboard.csproj'
$buildDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'bin')) + [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "找不到项目文件：$projectFile"
}

# 只关闭这个项目构建目录里的旧开发实例，不影响其他同名程序。
$stoppedIds = @()
Get-Process -Name 'OtpClipboard' -ErrorAction SilentlyContinue | ForEach-Object {
    $appProcess = $_
    try {
        $exePath = $appProcess.Path
        if ($exePath -and $exePath.StartsWith($buildDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
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
        -not $_.Path -or -not $_.Path.StartsWith($buildDirectory, [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch { $true }
}
if ($otherInstances) {
    throw '检测到另一处安装的“验证码剪贴板”正在运行。请先手动关闭它，再启动此开发实例。'
}

Write-Host '增量构建打包式 Debug 版本…'
dotnet build $projectFile -c Debug -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "构建失败，dotnet 退出码：$LASTEXITCODE"
}

Write-Host '启动开发实例…'
$startedAt = Get-Date
$launchLogDirectory = Join-Path ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) '.local'
New-Item -ItemType Directory -Force -Path $launchLogDirectory | Out-Null
$launchLog = Join-Path $launchLogDirectory ('launch-' + $startedAt.ToString('yyyyMMdd-HHmmss') + '.log')
$launchErrorLog = $launchLog + '.error.log'
$runner = Start-Process -FilePath (Get-Command dotnet).Source `
    -ArgumentList @('run', '--project', ('"' + $projectFile + '"'), '-c', 'Debug', '-p:Platform=x64', '--no-build', '--no-restore') `
    -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $launchLog -RedirectStandardError $launchErrorLog

$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    $instance = Get-Process -Name 'OtpClipboard' -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.StartTime -ge $startedAt -and $_.Path -and
                $_.Path.StartsWith($buildDirectory, [System.StringComparison]::OrdinalIgnoreCase) -and
                $_.MainWindowHandle -ne [IntPtr]::Zero
        }
        catch { $false }
    } | Select-Object -First 1

    if ($instance) {
        # A briefly visible window is not enough: startup exceptions can close it moments later.
        Start-Sleep -Seconds 2
        $instance.Refresh()
        if ($instance.HasExited -or $instance.MainWindowHandle -eq [IntPtr]::Zero) {
            throw "应用窗口出现后立即退出：PID $($instance.Id)"
        }
        Write-Host "开发实例已启动：PID $($instance.Id)"
        return
    }

    $runner.Refresh()
    if ($runner.HasExited) {
        $diagnostics = @(
            Get-Content -LiteralPath $launchLog -Encoding utf8 -Tail 20
            Get-Content -LiteralPath $launchErrorLog -Encoding utf8 -Tail 20
        ) -join "`n"
        throw "启动器提前退出，退出码：$($runner.ExitCode)`n$diagnostics`n完整日志：$launchLog"
    }
    Start-Sleep -Milliseconds 250
}

throw '30 秒内未检测到应用窗口，请检查 Windows 应用部署日志。'

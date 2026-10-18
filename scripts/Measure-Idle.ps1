<#
.SYNOPSIS
采样指定进程的 CPU、内存、线程和句柄，默认持续 30 秒。
.EXAMPLE
.\scripts\Measure-Idle.ps1 -ProcessId 12345 -Seconds 30
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$ProcessId,

    [ValidateRange(1, 86400)]
    [int]$Seconds = 30
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$ErrorActionPreference = 'Stop'

try {
    $sampleProcess = Get-Process -Id $ProcessId -ErrorAction Stop
}
catch {
    throw "无法打开进程 $ProcessId：$($_.Exception.Message)"
}

try {
    $sampleProcess.Refresh()
    $initialCpuMs = $sampleProcess.TotalProcessorTime.TotalMilliseconds
    $lastCpuMs = $initialCpuMs
    $lastElapsedMs = 0.0
    $sampleCount = 0
    $processorCount = [Environment]::ProcessorCount
    Write-Host "采样进程 $($sampleProcess.ProcessName)（PID $ProcessId），持续 $Seconds 秒。CPU 按 $processorCount 个逻辑处理器归一化；内存单位为 MiB。"
    $sampleClock = [System.Diagnostics.Stopwatch]::StartNew()

    while ($sampleClock.Elapsed.TotalSeconds -lt $Seconds) {
        $remainingMs = $Seconds * 1000.0 - $sampleClock.Elapsed.TotalMilliseconds
        Start-Sleep -Milliseconds ([int][Math]::Max(1, [Math]::Min(1000, [Math]::Ceiling($remainingMs))))

        try {
            $sampleProcess.Refresh()
            if ($sampleProcess.HasExited) {
                Write-Warning "进程 $ProcessId 已退出，结束采样。"
                break
            }
            $currentCpuMs = $sampleProcess.TotalProcessorTime.TotalMilliseconds
            $workingSetMiB = $sampleProcess.WorkingSet64 / 1MB
            $privateMiB = $sampleProcess.PrivateMemorySize64 / 1MB
            $threadCount = $sampleProcess.Threads.Count
            $handleCount = $sampleProcess.HandleCount
        }
        catch {
            Write-Warning "无法继续读取进程 $ProcessId，结束采样：$($_.Exception.Message)"
            break
        }

        $elapsedMs = $sampleClock.Elapsed.TotalMilliseconds
        $intervalMs = $elapsedMs - $lastElapsedMs
        $cpuPercent = 100.0 * [Math]::Max(0, $currentCpuMs - $lastCpuMs) / $intervalMs / $processorCount
        [pscustomobject][ordered]@{
            '进程ID' = $ProcessId
            '已用秒数' = [Math]::Round($elapsedMs / 1000.0, 2)
            'CPU百分比' = [Math]::Round($cpuPercent, 2)
            '工作集MiB' = [Math]::Round($workingSetMiB, 2)
            '私有内存MiB' = [Math]::Round($privateMiB, 2)
            '线程数' = $threadCount
            '句柄数' = $handleCount
        }
        $lastCpuMs = $currentCpuMs
        $lastElapsedMs = $elapsedMs
        $sampleCount++
    }

    $sampleClock.Stop()
    if ($sampleCount -gt 0) {
        $averageCpu = 100.0 * [Math]::Max(0, $lastCpuMs - $initialCpuMs) / $lastElapsedMs / $processorCount
        Write-Host ("采样结束：{0} 个样本，覆盖 {1:N2} 秒，平均 CPU {2:N2}%。" -f $sampleCount, ($lastElapsedMs / 1000.0), $averageCpu)
    }
    else {
        Write-Warning '未取得有效样本。'
    }
}
finally {
    $sampleProcess.Dispose()
}

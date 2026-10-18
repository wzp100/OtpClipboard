using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using OtpClipboard.Core;
using Windows.System;

namespace OtpClipboard.Services;

/// <summary>可空布尔值为 null 时表示查询失败，不能当作未安装或未运行。</summary>
public sealed record PhoneLinkStatus(bool? IsInstalled, bool? IsRunning, string Message, Exception? Error = null);

/// <summary>成功仅表示系统接受了打开请求，不代表手机已连接；回退成功时 Error 可包含先前失败原因。</summary>
public sealed record PhoneLinkLaunchResult(bool Succeeded, string Message, Exception? Error = null);

public static class PhoneLinkService
{
    public const string PackageFamilyName = NotificationSource.PhoneLinkPackageFamilyName;
    private const string ConnectionNote = "进程状态不代表手机已连接，请在“手机连接”中确认实际连接状态。";

    /// <summary>仅在用户点击“刷新”时调用；后台检查当前用户安装及当前会话进程，没有缓存或轮询。</summary>
    public static Task<PhoneLinkStatus> RefreshAsync() => Task.Run(() =>
    {
        var errors = new List<Exception>();
        bool? installed = null;
        bool? running = null;
        try
        {
            installed = FindPackages().Length > 0;
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            int sessionId = currentProcess.SessionId;
            bool found = false;
            bool failed = false;
            foreach (string name in new[] { "PhoneExperienceHost", "YourPhone" })
            {
                try
                {
                    var processes = Process.GetProcessesByName(name);
                    try
                    {
                        foreach (var process in processes)
                        {
                            // 查询期间进程退出属于正常竞争，不视为仍在运行。
                            try
                            {
                                if (!process.HasExited && process.SessionId == sessionId) found = true;
                            }
                            catch (InvalidOperationException) { }
                        }
                    }
                    finally
                    {
                        foreach (var process in processes) process.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    failed = true;
                    errors.Add(ex);
                }
            }
            running = found ? true : failed ? null : false;
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        string installMessage = installed switch
        {
            true => "当前用户已安装手机连接",
            false => "当前用户未安装手机连接",
            null => "无法确认手机连接是否安装",
        };
        string processMessage = running switch
        {
            true => "当前会话检测到手机连接进程",
            false => "当前会话未检测到手机连接进程",
            null => "无法确认手机连接进程状态",
        };
        return new PhoneLinkStatus(installed, running, $"{installMessage}；{processMessage}。{ConnectionNote}", CombineErrors(errors));
    });

    /// <summary>用户点击“打开手机连接”时从 UI 线程 await 调用；优先包入口，失败后尝试 ms-phone:。</summary>
    public static async Task<PhoneLinkLaunchResult> OpenAsync()
    {
        var errors = new List<Exception>();
        try
        {
            var packages = await Task.Run(FindPackages);
            foreach (var package in packages)
            {
                try
                {
                    var entries = await Task.Run(async () => await package.GetAppListEntriesAsync());
                    foreach (var entry in entries)
                    {
                        try
                        {
                            if (await entry.LaunchAsync())
                                return new PhoneLinkLaunchResult(true, $"已通过应用入口请求打开手机连接。{ConnectionNote}", CombineErrors(errors));
                            errors.Add(new InvalidOperationException("手机连接应用入口拒绝了启动请求。"));
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            // 限定到手机连接包，避免协议被其他应用接管时报告打开成功。
            var options = new LauncherOptions { TargetApplicationPackageFamilyName = PackageFamilyName };
            if (await Launcher.LaunchUriAsync(new Uri("ms-phone:"), options))
                return new PhoneLinkLaunchResult(true, $"已通过 ms-phone 协议请求打开手机连接。{ConnectionNote}", CombineErrors(errors));
            errors.Add(new InvalidOperationException("系统未接受 ms-phone 协议启动请求。"));
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        return new PhoneLinkLaunchResult(false, "无法打开手机连接，请确认当前用户已安装该应用后重试。", CombineErrors(errors));
    }

    // 空 SID 表示当前用户，避免跨用户枚举要求管理员权限。枚举必须在 Task.Run 内完成。
    private static Package[] FindPackages() => new PackageManager()
        .FindPackagesForUser(string.Empty, PackageFamilyName).ToArray();

    private static Exception? CombineErrors(List<Exception> errors) => errors.Count switch
    {
        0 => null,
        1 => errors[0],
        _ => new AggregateException("手机连接操作中发生多个错误。", errors),
    };
}

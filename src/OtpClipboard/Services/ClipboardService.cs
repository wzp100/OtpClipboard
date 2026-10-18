using OtpClipboard.Core;
using Windows.ApplicationModel.DataTransfer;

namespace OtpClipboard.Services;

/// <summary>Success 为 false 时 Error 说明原因；FlushWarning 非空表示已写入但程序退出后内容可能失效。</summary>
public readonly record struct ClipboardResult(bool Success, string? Error, string? FlushWarning);

/// <summary>写入剪贴板。必须在 UI 线程调用。</summary>
public static class ClipboardService
{
    private const int Attempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(80);

    /// <summary>
    /// 其他程序短暂占用剪贴板时 OpenClipboard 会失败（CLIPBRD_E_CANT_OPEN），因此写入会重试几次。
    /// cancellation 触发后不再开始新的写入尝试。
    /// </summary>
    public static async Task<ClipboardResult> TryCopyAsync(string text, CancellationToken cancellation = default)
    {
        Exception? lastFailure = null;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            if (cancellation.IsCancellationRequested) return new(false, "程序正在退出", null);
            try
            {
                var package = new DataPackage();
                package.SetText(text);
                // 避免验证码进入 Windows 剪贴板历史或跨设备同步。
                if (!Clipboard.SetContentWithOptions(package, new ClipboardContentOptions
                {
                    IsAllowedInHistory = false,
                    IsRoamable = false,
                }))
                    throw new InvalidOperationException("系统未接受剪贴板内容。");
                lastFailure = null;
                break;
            }
            catch (Exception ex)
            {
                lastFailure = ex;
                if (attempt < Attempts - 1) await Task.Delay(RetryDelay);
            }
        }

        if (lastFailure is not null) return new(false, ErrorText.Describe(lastFailure), null);

        string? flushWarning = null;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                Clipboard.Flush();
                flushWarning = null;
                break;
            }
            catch (Exception ex)
            {
                // Flush 只影响程序退出后内容是否保留，复制本身已经成功。
                flushWarning = ErrorText.Describe(ex);
                if (attempt < Attempts - 1) await Task.Delay(RetryDelay);
            }
        }

        return new(true, null, flushWarning);
    }
}

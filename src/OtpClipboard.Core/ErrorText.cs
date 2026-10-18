namespace OtpClipboard.Core;

/// <summary>把异常转换成显示在处理记录里的中文说明。</summary>
public static class ErrorText
{
    /// <summary>CLIPBRD_E_CANT_OPEN：其他程序短暂占用了剪贴板，系统不会给出文字描述。</summary>
    public const int ClipboardBusyHResult = unchecked((int)0x800401D0);

    public static string Describe(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (ex.HResult == ClipboardBusyHResult)
        {
            return "剪贴板被其他程序短暂占用（COMException 0x800401D0）";
        }

        string message = string.IsNullOrWhiteSpace(ex.Message) ? "（系统未提供错误描述）" : ex.Message;
        return $"{ex.GetType().Name} 0x{ex.HResult:X8}：{message}";
    }
}

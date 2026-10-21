namespace OtpClipboard.Models;

/// <summary>
/// 后台处理记录。此类型不引用 WinUI 对象，可在通知线程和后台消息循环之间安全传递。
/// </summary>
public sealed record MonitorRecord(
    string Kind,
    string StatusText,
    string Title,
    string Detail,
    string? Code,
    string Time);

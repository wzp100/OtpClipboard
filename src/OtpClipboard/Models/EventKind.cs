namespace OtpClipboard.Models;

/// <summary>处理记录的结果分类。名称会写入历史记录文件，改名会导致旧记录读成 Info。</summary>
public enum EventKind
{
    Info,
    Copied,
    NoMatch,
    Filtered,
    Failed,
}

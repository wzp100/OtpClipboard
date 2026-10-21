using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OtpClipboard.Models;

/// <summary>“处理记录”列表中的一行。</summary>
public sealed class EventItem
{
    public EventKind Kind { get; init; }
    public string StatusText { get; init; } = "";
    public Brush? StatusBrush => EventStatusBrushes.For(Kind);
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    /// <summary>识别出的验证码，供“复制”按钮重新复制；没有时为 null。</summary>
    public string? Code { get; init; }
    public string Time { get; init; } = "";

    public Visibility CopyVisibility => string.IsNullOrEmpty(Code) ? Visibility.Collapsed : Visibility.Visible;

    public static EventItem Create(EventKind kind, string statusText, string title, string detail,
        string? code = null, string? time = null) => new()
    {
        Kind = kind,
        StatusText = statusText,
        Title = title,
        Detail = detail,
        Code = code,
        Time = time ?? "",
    };

    public static EventItem CreateRestored(EventKind kind, string time, string title = "",
        string detail = "") => Create(
            kind,
            kind switch
            {
                EventKind.Copied => "已复制",
                EventKind.NoMatch => "未匹配",
                EventKind.Filtered => "已过滤",
                EventKind.Failed => "失败",
                _ => "提示",
            },
            string.IsNullOrEmpty(title) ? "上次运行的处理记录" : title,
            string.IsNullOrEmpty(detail)
                ? "为保护隐私，重启后仅恢复状态和时间，不保留通知内容或验证码。"
                : detail,
            time: time);
}

/// <summary>
/// 状态颜色只在 UI 进程按需创建；后台记录不持有 WinUI 对象。
/// </summary>
internal static class EventStatusBrushes
{
    private static readonly Dictionary<EventKind, SolidColorBrush> Brushes = new()
    {
        [EventKind.Copied] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x7B, 0x0F)),
        [EventKind.NoMatch] = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x5D, 0x00)),
        [EventKind.Filtered] = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x6B, 0x6B)),
        [EventKind.Failed] = new SolidColorBrush(Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)),
        [EventKind.Info] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x6C, 0xBD)),
    };

    public static SolidColorBrush For(EventKind kind) => Brushes[kind];
}

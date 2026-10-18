using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OtpClipboard.Models;

/// <summary>“处理记录”列表中的一行。</summary>
public sealed class EventItem
{
    public EventKind Kind { get; init; }
    public string StatusText { get; init; } = "";
    public Brush? StatusBrush { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    /// <summary>识别出的验证码，供“复制”按钮重新复制；没有时为 null。</summary>
    public string? Code { get; init; }
    public string Time { get; init; } = "";

    public Visibility CopyVisibility => string.IsNullOrEmpty(Code) ? Visibility.Collapsed : Visibility.Visible;
}

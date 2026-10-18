namespace OtpClipboard.Core;

/// <summary>判断通知是否来自“手机连接”，并生成记录里显示的来源文字。</summary>
public static class NotificationSource
{
    /// <summary>“手机连接 / Phone Link”的包系列名。按包身份判断，不依赖可能随系统语言变化的显示名称。</summary>
    public const string PhoneLinkPackageFamilyName = "Microsoft.YourPhone_8wekyb3d8bbwe";

    public static bool IsPhoneLink(string? packageFamily, string? appUserModelId) =>
        string.Equals(packageFamily, PhoneLinkPackageFamilyName, StringComparison.OrdinalIgnoreCase) ||
        (appUserModelId?.StartsWith(PhoneLinkPackageFamilyName + "!", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>优先显示应用名；非手机连接来源再附上包系列名，便于排查“已过滤”的记录。</summary>
    public static string Describe(string? appName, string? packageFamily)
    {
        string origin = !string.IsNullOrWhiteSpace(appName)
            ? appName
            : !string.IsNullOrWhiteSpace(packageFamily) ? packageFamily : "未知应用";
        if (!string.IsNullOrWhiteSpace(packageFamily) &&
            !packageFamily.Equals(origin, StringComparison.OrdinalIgnoreCase) &&
            !IsPhoneLink(packageFamily, null))
        {
            origin += $"（{packageFamily}）";
        }
        return origin;
    }
}

using System.Globalization;
using System.Text.Json;

namespace OtpClipboard.Core;

/// <summary>磁盘历史仅允许状态分类和时间；禁止添加验证码、标题或通知正文。</summary>
public sealed record HistoryEntry(string Kind, string Time);

/// <summary>历史文件的隐私边界。兼容旧 JSON，读取和写入都只保留白名单元数据。</summary>
public static class HistoryArchive
{
    public const int MaxRecords = 200;
    public const string TimeFormat = "MM-dd HH:mm:ss";

    public static List<HistoryEntry> Deserialize(string json) =>
        Sanitize(JsonSerializer.Deserialize<List<HistoryEntry?>>(json) ?? []);

    public static string Serialize(IEnumerable<HistoryEntry> entries) =>
        JsonSerializer.Serialize(Sanitize(entries));

    private static List<HistoryEntry> Sanitize(IEnumerable<HistoryEntry?> entries) => entries
        .Where(entry => entry is not null
            && entry.Kind is "Info" or "Copied" or "NoMatch" or "Filtered" or "Failed"
            && DateTime.TryParseExact("2000-" + entry.Time, "yyyy-" + TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
        .Take(MaxRecords)
        .Select(entry => entry!)
        .ToList();
}

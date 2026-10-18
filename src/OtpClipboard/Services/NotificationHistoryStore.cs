using System.Diagnostics;
using System.Text;
using OtpClipboard.Core;

namespace OtpClipboard.Services;

/// <summary>串行读写脱敏历史，先写临时文件再替换。读取旧文件时立即移除敏感字段。</summary>
public static class NotificationHistoryStore
{
    public const string FileName = "notification-history.json";
    public const int MaxRecords = HistoryArchive.MaxRecords;
    private static readonly SemaphoreSlim FileGate = new(1, 1);
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static async Task<List<HistoryEntry>> LoadAsync()
    {
        await FileGate.WaitAsync();
        try
        {
            string path = FilePath();
            if (!File.Exists(path)) return [];
            string json = await File.ReadAllTextAsync(path, Utf8);
            var entries = HistoryArchive.Deserialize(json);
            // 不在内存恢复旧正文或验证码，并在返回前覆盖旧文件。
            await WriteAsync(path, HistoryArchive.Serialize(entries));
            return entries;
        }
        finally { FileGate.Release(); }
    }

    public static async Task SaveAsync(IReadOnlyList<HistoryEntry> entries)
    {
        string json = HistoryArchive.Serialize(entries);
        await FileGate.WaitAsync();
        try { await WriteAsync(FilePath(), json); }
        finally { FileGate.Release(); }
    }

    private static async Task WriteAsync(string path, string json)
    {
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, Utf8);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"清理历史记录临时文件失败：{ex.GetType().Name}");
            }
        }
    }

    private static string FilePath() => Path.Combine(
        Windows.Storage.ApplicationData.Current.LocalFolder.Path, FileName);
}

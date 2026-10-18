using Microsoft.UI.Xaml;
using OtpClipboard.Core;
using OtpClipboard.Models;
using OtpClipboard.Services;

namespace OtpClipboard.Views;

// 历史记录的筛选、展示和持久化；通知监听与设置交互保留在 MainPage.xaml.cs。
public sealed partial class MainPage
{
    private EventKind? ActiveFilter => RecordFilter.SelectedIndex switch
    {
        1 => EventKind.Copied,
        2 => EventKind.NoMatch,
        3 => EventKind.Filtered,
        4 => EventKind.Failed,
        _ => null,
    };

    private void AddRecord(EventKind kind, string statusText, string title, string detail, string? code = null, DateTime? timestamp = null)
    {
        var item = new EventItem
        {
            Kind = kind,
            StatusText = statusText,
            StatusBrush = _statusBrushes[kind],
            Title = title,
            Detail = detail,
            Code = code,
            Time = (timestamp ?? DateTime.Now).ToString(HistoryArchive.TimeFormat, System.Globalization.CultureInfo.InvariantCulture),
        };

        _records.Insert(0, item);
        while (_records.Count > MaxRecords)
        {
            var oldest = _records[^1];
            _records.RemoveAt(_records.Count - 1);
            _visible.Remove(oldest);
        }

        var filter = ActiveFilter;
        if (filter is null || filter == kind) _visible.Insert(0, item);

        UpdateSummary();
        ScheduleSave();
    }

    private void RebuildVisible()
    {
        if (EventList is null) return;
        _visible.Clear();
        var filter = ActiveFilter;
        foreach (var record in _records)
        {
            if (filter is null || record.Kind == filter) _visible.Add(record);
        }
    }

    private void UpdateSummary()
    {
        if (RecordSummary is null) return;

        int copied = 0, noMatch = 0, filtered = 0, failed = 0;
        foreach (var record in _records)
        {
            switch (record.Kind)
            {
                case EventKind.Copied: copied++; break;
                case EventKind.NoMatch: noMatch++; break;
                case EventKind.Filtered: filtered++; break;
                case EventKind.Failed: failed++; break;
            }
        }

        UpdateLatestCode();
        EmptyHistory.Visibility = _visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHistoryText.Text = _records.Count == 0 ? "还没有记录。新通知到达时会在这里逐条显示处理结果。" : "没有符合筛选条件的记录";

        string state = _monitoring ? "监听中" : "未监听";
        if (_records.Count == 0)
        {
            RecordSummary.Text = state;
            return;
        }

        string summary = $"{state} · 共 {_records.Count} 条：已复制 {copied}，未匹配 {noMatch}，已过滤 {filtered}，失败 {failed}";
        if (ActiveFilter is not null) summary += $" · 当前显示 {_visible.Count} 条";
        RecordSummary.Text = summary;
    }

    private EventItem? LatestCopied() =>
        _records.FirstOrDefault(record => record.Kind == EventKind.Copied && !string.IsNullOrEmpty(record.Code));

    private void UpdateLatestCode()
    {
        var latest = LatestCopied();
        LatestCopyButton.IsEnabled = latest is not null;
        LatestCode.Text = latest?.Code ?? "——";
        LatestCodeDetail.Text = latest is null
            ? "还没有识别到验证码。收到后会自动复制，并在输入光标旁提示。"
            : $"{latest.Time} · {latest.Title}";
    }

    private async Task LoadHistoryAsync()
    {
        if (_historyLoaded) return;
        _historyLoaded = true;

        try
        {
            foreach (var entry in await NotificationHistoryStore.LoadAsync())
            {
                if (!Enum.TryParse<EventKind>(entry.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind)) kind = EventKind.Info;
                _records.Add(new EventItem
                {
                    Kind = kind,
                    StatusText = kind switch
                    {
                        EventKind.Copied => "已复制",
                        EventKind.NoMatch => "未匹配",
                        EventKind.Filtered => "已过滤",
                        EventKind.Failed => "失败",
                        _ => "提示",
                    },
                    StatusBrush = _statusBrushes[kind],
                    Title = "上次运行的处理记录",
                    Detail = "为保护隐私，重启后仅恢复状态和时间，不保留通知内容或验证码。",
                    Time = entry.Time,
                });
            }

            RebuildVisible();
            UpdateSummary();
        }
        catch (Exception ex)
        {
            AddRecord(EventKind.Failed, "失败", "读取历史记录失败", DescribeException(ex));
        }
    }

    private void ScheduleSave()
    {
        if (!_historyLoaded) return;
        if (_saveTimer is null)
        {
            _ = SaveHistoryAsync();
            return;
        }
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async Task SaveHistoryAsync()
    {
        if (!_historyLoaded) return;
        await _saveLock.WaitAsync();
        try
        {
            var payload = _records
                .Select(record => new HistoryEntry(record.Kind.ToString(), record.Time))
                .ToList();
            await NotificationHistoryStore.SaveAsync(payload);
        }
        catch (Exception ex)
        {
            if (_saveFailedReported) return;
            _saveFailedReported = true;
            AddRecord(EventKind.Failed, "失败", "保存记录失败", DescribeException(ex));
        }
        finally { _saveLock.Release(); }
    }

}

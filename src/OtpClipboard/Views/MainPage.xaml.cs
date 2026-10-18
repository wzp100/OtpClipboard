using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OtpClipboard.Core;
using OtpClipboard.Models;
using OtpClipboard.Services;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace OtpClipboard.Views;

/// <summary>
/// 主界面：概览、处理记录、设置三个标签页。
/// 负责监听通知、把识别出的验证码交给 ClipboardService，并维护处理记录。
/// </summary>
public sealed partial class MainPage : Page
{
    private const int MaxRecords = NotificationHistoryStore.MaxRecords;

    private readonly UserNotificationListener _listener = UserNotificationListener.Current;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HashSet<uint> _seenNotificationIds = [];
    private readonly List<EventItem> _records = [];
    private readonly ObservableCollection<EventItem> _visible = [];
    private readonly Dictionary<EventKind, SolidColorBrush> _statusBrushes = new()
    {
        [EventKind.Copied] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x7B, 0x0F)),
        [EventKind.NoMatch] = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x5D, 0x00)),
        [EventKind.Filtered] = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x6B, 0x6B)),
        [EventKind.Failed] = new SolidColorBrush(Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)),
        [EventKind.Info] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x6C, 0xBD)),
    };

    private DispatcherQueueTimer? _saveTimer;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly Queue<uint> _seenOrder = new();
    private readonly Queue<UserNotification> _pending = new();
    private bool _processing;
    private bool _closing;
    private bool _initializing = true;
    private bool _initialized;
    private bool _refreshingPhone;
    public MainWindow? Host { get; set; }
    public AppPreferences Preferences { get; private set; } = new();
    private bool _monitoring;
    private bool _ready;
    private bool _historyLoaded;
    private bool _saveFailedReported;

    public MainPage()
    {
        InitializeComponent();
        EventList.ItemsSource = _visible;
        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromSeconds(2);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += async (_, _) => await SaveHistoryAsync();

        PhoneLinkOnlyToggle.IsOn = Preferences.PhoneLinkOnly;
        PopupToggle.IsOn = Preferences.ShowPopup;
        TrayToggle.IsOn = Preferences.MinimizeToTray;
        _initializing = false;
        _ready = true;
        SetStatus(StatusKind.Neutral, "正在检查通知权限", "首次使用请授权通知访问。");
        UpdateSummary();
    }

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (OverviewPanel is null || HistoryPanel is null || SettingsPanel is null) return;
        var selected = sender.SelectedItem;
        OverviewPanel.Visibility = selected == OverviewTab ? Visibility.Visible : Visibility.Collapsed;
        HistoryPanel.Visibility = selected == HistoryTab ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = selected == SettingsTab ? Visibility.Visible : Visibility.Collapsed;
    }

    private enum StatusKind { Neutral, Success, Caution }

    private void SetStatus(StatusKind kind, string title, string message)
    {
        StatusTitle.Text = title;
        StatusMessage.Text = message;
        StatusGlyph.Glyph = kind switch
        {
            StatusKind.Success => "\uE73E", // CheckMark
            StatusKind.Caution => "\uE7BA", // Warning
            _ => "\uE946", // Info
        };
        VisualStateManager.GoToState(this, "Status" + kind, false);
    }

    private void SetMonitorButton(bool monitoring)
    {
        MonitorGlyph.Glyph = monitoring ? "\uE71A" : "\uE768"; // Stop / Play
        MonitorLabel.Text = monitoring ? "停止监听" : "开始监听";
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await LoadHistoryAsync();
        try { Preferences = await AppPreferences.LoadAsync(); }
        catch (Exception ex) { ReportStatus($"设置读取失败，暂用默认值：{ex.Message}"); }
        _initializing = true;
        PhoneLinkOnlyToggle.IsOn = Preferences.PhoneLinkOnly;
        PopupToggle.IsOn = Preferences.ShowPopup;
        TrayToggle.IsOn = Preferences.MinimizeToTray;
        _initializing = false;
        if (_closing) return;
        try
        {
            var status = _listener.GetAccessStatus();
            UpdateAccessUi(status);
            if (status == UserNotificationListenerAccessStatus.Allowed)
            {
                StartMonitoring();
            }
        }
        catch (Exception ex)
        {
            AddRecord(EventKind.Failed, "失败", "自动监听启动失败", DescribeException(ex));
        }
        await Task.WhenAll(RefreshPhoneAsync(), RefreshStartupAsync());
    }

    private async void AuthorizeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var status = await _listener.RequestAccessAsync();
            bool allowed = status == UserNotificationListenerAccessStatus.Allowed;
            UpdateAccessUi(status);
            AddRecord(
                allowed ? EventKind.Info : EventKind.Failed,
                allowed ? "已授权" : "失败",
                "通知读取授权",
                allowed ? "已允许，正在自动开启监听。" : "未获得权限，请在 Windows 设置中允许后重试。");
            if (allowed) StartMonitoring();
        }
        catch (Exception ex) { ReportStatus($"无法申请通知权限：{DescribeException(ex)}"); }
    }

    private void MonitorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
        {
            UpdateAccessUi(_listener.GetAccessStatus());
            return;
        }

        if (!_monitoring)
        {
            StartMonitoring();
        }
        else
        {
            StopMonitoring();
            SetStatus(StatusKind.Caution, "监听已停止", "新的验证码不会被自动复制。点击“开始监听”恢复。");
        }
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
        {
            AddRecord(EventKind.Failed, "失败", "无法扫描", "请先点击“授权通知访问”。");
            return;
        }
        await ScanCurrentNotificationsAsync();
        Tabs.SelectedItem = HistoryTab;
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearFlyout.Hide();
        _records.Clear();
        _visible.Clear();
        // 清空之后允许“扫描当前通知”重新读一遍通知中心，用来做诊断。
        _seenNotificationIds.Clear();
        _seenOrder.Clear();
        UpdateSummary();
        await SaveHistoryAsync();
    }

    private async void CopyRecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EventItem item }) await RecopyAsync(item);
    }

    private async void LatestCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (LatestCopied() is { } item) await RecopyAsync(item);
    }

    private async Task RecopyAsync(EventItem item)
    {
        if (string.IsNullOrEmpty(item.Code)) return;

        var (success, error, flushWarning) = await TryCopyToClipboardAsync(item.Code);
        if (!success)
        {
            AddRecord(EventKind.Failed, "失败", "重新复制失败", error ?? "未知错误", item.Code);
            return;
        }

        string detail = $"来自这条记录：{item.Title}（{item.Time}）";
        if (flushWarning is not null) detail += $"\n注意：Flush 失败（{flushWarning}）";
        AddRecord(EventKind.Copied, "已复制", $"已重新复制验证码 {Mask(item.Code)}", detail, item.Code);
    }

    private void RecordFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        RebuildVisible();
        UpdateSummary();
    }

    private void StartMonitoring()
    {
        if (_monitoring || _listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed) return;
        try
        {
            _listener.NotificationChanged += OnNotificationChanged;
        }
        catch (Exception ex)
        {
            AddRecord(EventKind.Failed, "失败", "监听启动失败", DescribeException(ex));
            return;
        }
        _monitoring = true;
        SetMonitorButton(true);
        SetStatus(StatusKind.Success, "正在监听", "等待“手机连接”把手机通知送入 Windows 通知中心，识别到验证码会自动复制。");
        UpdateSummary();
    }

    private void UpdateAccessUi(UserNotificationListenerAccessStatus status)
    {
        bool allowed = status == UserNotificationListenerAccessStatus.Allowed;
        MonitorButton.IsEnabled = allowed;
        AuthorizeButton.Visibility = allowed ? Visibility.Collapsed : Visibility.Visible;
        if (allowed) SetStatus(StatusKind.Neutral, "通知访问已授权", "点击“开始监听”，之后新验证码会自动复制。");
        else SetStatus(StatusKind.Caution, "需要通知访问权限", "点击“授权通知访问”，并在系统提示中选择允许。");
    }

    private void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        uint notificationId = args.UserNotificationId;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (args.ChangeKind != UserNotificationChangedKind.Added) return;
            if (!_monitoring || _closing) return;
            try
            {
                if (_listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
                {
                    StopMonitoring();
                    UpdateAccessUi(_listener.GetAccessStatus());
                    return;
                }
                if (!RememberNotification(notificationId)) return;
                var notification = _listener.GetNotification(notificationId);
                if (notification is not null) QueueNotification(notification);
            }
            catch (Exception ex)
            {
                AddRecord(EventKind.Failed, "失败", $"读取新通知失败（ID {notificationId}）", DescribeException(ex));
            }
        });
    }

    private void StopMonitoring()
    {
        if (!_monitoring) return;
        _monitoring = false;
        _pending.Clear();
        Host?.DismissPopup();
        _listener.NotificationChanged -= OnNotificationChanged;
        SetMonitorButton(false);
        UpdateSummary();
    }

    private async Task ScanCurrentNotificationsAsync()
    {
        try
        {
            var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            int newlyHandled = 0;
            foreach (var notification in notifications.OrderBy(n => n.CreationTime))
            {
                if (!RememberNotification(notification.Id)) continue;
                QueueNotification(notification);
                newlyHandled++;
            }
            AddRecord(
                EventKind.Info,
                "扫描完成",
                $"通知中心共 {notifications.Count} 条通知",
                $"本次新处理 {newlyHandled} 条，其余 {notifications.Count - newlyHandled} 条已在记录中（不会重复处理）。");
        }
        catch (Exception ex)
        {
            AddRecord(EventKind.Failed, "失败", "读取通知失败", DescribeException(ex));
        }
    }

    private bool RememberNotification(uint id)
    {
        if (!_seenNotificationIds.Add(id)) return false;
        _seenOrder.Enqueue(id);
        while (_seenOrder.Count > 512) _seenNotificationIds.Remove(_seenOrder.Dequeue());
        return true;
    }

    private async void QueueNotification(UserNotification notification)
    {
        // 限制突发通知队列，避免长期后台积压；正常逐条串行处理，保证剪贴板顺序。
        if (_pending.Count >= 200) _pending.Dequeue();
        _pending.Enqueue(notification);
        if (_processing) return;
        _processing = true;
        try
        {
            while (!_closing && _pending.TryDequeue(out var next))
            {
                try { await ProcessNotificationAsync(next); }
                catch (Exception ex) { ReportStatus($"处理通知失败：{DescribeException(ex)}"); }
            }
        }
        finally { _processing = false; }
    }

    private async Task ProcessNotificationAsync(UserNotification notification)
    {
        string appName = notification.AppInfo?.DisplayInfo?.DisplayName ?? "";
        string appId = notification.AppInfo?.AppUserModelId ?? "";
        string packageFamily = notification.AppInfo?.PackageFamilyName ?? "";
        string origin = NotificationSource.Describe(appName, packageFamily);
        string time = notification.CreationTime.LocalDateTime.ToString(HistoryArchive.TimeFormat, System.Globalization.CultureInfo.InvariantCulture);
        string context = $"来源：{origin}　时间：{time}";

        if (PhoneLinkOnlyToggle.IsOn && !NotificationSource.IsPhoneLink(packageFamily, appId))
        {
            AddRecord(EventKind.Filtered, "已过滤", "来源不是手机连接，未做处理", context, null, notification.CreationTime.LocalDateTime);
            return;
        }

        // 先检查来源，避免为已过滤的应用读取通知正文。
        string body = ReadNotificationText(notification);
        string preview = OtpParser.Preview(body);
        context += preview.Length > 0 ? $"\n内容：{preview}" : "\n内容：（通知没有可读取的文本）";

        if (body.Length == 0)
        {
            AddRecord(EventKind.NoMatch, "未匹配", "通知里没有文本内容", context, null, notification.CreationTime.LocalDateTime);
            return;
        }

        string? code = OtpParser.Extract(body);
        if (code is null)
        {
            AddRecord(EventKind.NoMatch, "未匹配", "通知里没有识别到验证码", context, null, notification.CreationTime.LocalDateTime);
            return;
        }

        var (success, copyError, flushWarning) = await TryCopyToClipboardAsync(code);
        if (_closing) return;
        if (!success)
        {
            AddRecord(EventKind.Failed, "失败", $"识别到验证码 {Mask(code)}，但写入剪贴板失败", $"{context}\n{copyError}", code, notification.CreationTime.LocalDateTime);
            return;
        }

        string copiedDetail = context;
        if (flushWarning is not null)
        {
            copiedDetail += $"\n注意：剪贴板内容已写入，但 Flush 失败，程序退出后可能失效（{flushWarning}）";
        }
        AddRecord(EventKind.Copied, "已复制", $"验证码 {Mask(code)} 已写入剪贴板", copiedDetail, code, notification.CreationTime.LocalDateTime);
        // 手动扫描较早的通知只复制，不把过期验证码弹到当前输入框旁。
        if (DateTimeOffset.Now - notification.CreationTime < TimeSpan.FromMinutes(2)) Host?.ShowCode(code);
    }

    private Task<ClipboardResult> TryCopyToClipboardAsync(string text) =>
        ClipboardService.TryCopyAsync(text, _shutdown.Token);

    private static string DescribeException(Exception ex) => ErrorText.Describe(ex);

    private static string ReadNotificationText(UserNotification notification)
    {
        var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
        if (binding is null) return "";
        return string.Join(
            "\n",
            binding.GetTextElements()
                   .Select(element => element.Text)
                   .Where(text => !string.IsNullOrWhiteSpace(text)))
            .Trim();
    }

    public async Task ShutdownAsync()
    {
        _closing = true;
        _shutdown.Cancel();
        StopMonitoring();
        _pending.Clear();
        _saveTimer?.Stop();
        await SaveHistoryAsync();
    }

    public void ReportStatus(string message) => AddRecord(EventKind.Info, "提示", message, DateTime.Now.ToString("HH:mm:ss"));

    public void RefreshAccess()
    {
        if (!_initialized || _closing) return;
        try
        {
            var status = _listener.GetAccessStatus();
            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                StopMonitoring();
                UpdateAccessUi(status);
            }
            else if (!_monitoring) UpdateAccessUi(status);
        }
        catch (Exception ex) { ReportStatus($"无法检查通知权限：{ex.Message}"); }
    }

    private async void Preference_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready || _initializing) return;
        Preferences.PhoneLinkOnly = PhoneLinkOnlyToggle.IsOn;
        Preferences.ShowPopup = PopupToggle.IsOn;
        Preferences.MinimizeToTray = TrayToggle.IsOn;
        if (!Preferences.ShowPopup) Host?.DismissPopup();
        try { await Preferences.SaveAsync(); } catch (Exception ex) { ReportStatus($"设置保存失败：{ex.Message}"); }
    }

    private async Task RefreshStartupAsync()
    {
        try { ApplyStartupState(await StartupService.QueryAsync()); }
        catch (Exception ex) { StartupDetail.Text = $"无法读取启动设置：{ex.Message}"; }
    }

    private void ApplyStartupState(StartupStatus state)
    {
        _initializing = true;
        StartupToggle.IsOn = state.IsEnabled == true;
        StartupToggle.IsEnabled = state.CanChange;
        StartupDetail.Text = state.Message;
        _initializing = false;
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready || _initializing) return;
        bool requested = StartupToggle.IsOn;
        StartupToggle.IsEnabled = false;
        try { ApplyStartupState(await StartupService.SetEnabledAsync(requested)); }
        catch (Exception ex)
        {
            StartupDetail.Text = $"无法修改自动启动：{ex.Message}";
            await RefreshStartupAsync();
        }
    }

    private async Task RefreshPhoneAsync()
    {
        if (_refreshingPhone || _closing) return;
        _refreshingPhone = true;
        RefreshPhoneButton.IsEnabled = false;
        try
        {
            var state = await PhoneLinkService.RefreshAsync();
            PhoneLinkStatus.Text = state.IsRunning == true ? "手机连接 · 应用正在运行" : state.IsInstalled == false ? "手机连接 · 未检测到安装" : state.IsRunning == false ? "手机连接 · 应用未运行" : "手机连接 · 状态未知";
            PhoneLinkDetail.Text = state.Message;
        }
        catch (Exception ex) { PhoneLinkDetail.Text = $"检测失败：{ex.Message}"; }
        finally { _refreshingPhone = false; RefreshPhoneButton.IsEnabled = true; }
    }

    private async void RefreshPhoneButton_Click(object sender, RoutedEventArgs e) => await RefreshPhoneAsync();

    private async void OpenPhoneButton_Click(object sender, RoutedEventArgs e)
    {
        OpenPhoneButton.IsEnabled = false;
        try
        {
            if (!(await PhoneLinkService.OpenAsync()).Succeeded) ReportStatus("未能打开手机连接，请从开始菜单打开或安装该应用。");
            await RefreshPhoneAsync();
        }
        catch (Exception ex) { ReportStatus($"打开手机连接失败：{ex.Message}"); }
        finally { OpenPhoneButton.IsEnabled = true; }
    }

    private async void StartupSettings_Click(object sender, RoutedEventArgs e)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps")); }
        catch (Exception ex) { ReportStatus($"无法打开系统设置：{ex.Message}"); }
    }

    private void TrayButton_Click(object sender, RoutedEventArgs e) => Host?.HideToTray();
    private async void ExitButton_Click(object sender, RoutedEventArgs e) { if (Host is not null) await Host.ExitAsync(); }
    private void PreviewPopup_Click(object sender, RoutedEventArgs e)
    {
        Host?.ShowPreview("246810");
    }

    private static string Mask(string code) => OtpParser.Mask(code);
}

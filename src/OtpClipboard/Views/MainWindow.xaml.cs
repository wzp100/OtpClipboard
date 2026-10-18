using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using OtpClipboard.Desktop;

namespace OtpClipboard.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainPage _page;
    private DesktopIntegration? _desktop;
    private bool _exiting;
    private bool _hidden;

    public MainWindow()
    {
        InitializeComponent();
        // AppWindow 使用物理像素；按显示缩放设置初始大小，避免高 DPI 下列表被挤没。
        double scale = DesktopNativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Math.Min((int)(760 * scale), work.Width - 40), Math.Min((int)(720 * scale), work.Height - 40)));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        RootFrame.Navigate(typeof(MainPage));
        _page = (MainPage)RootFrame.Content;
        _page.Host = this;
        try
        {
            _desktop = new DesktopIntegration(WinRT.Interop.WindowNative.GetWindowHandle(this), ShowMain,
                () => _ = ExitAsync(), message => _page.ReportStatus(message));
        }
        catch (Exception ex)
        {
            _page.ReportStatus($"托盘初始化失败，将保留主窗口：{ex.Message}");
        }
        AppWindow.Closing += (sender, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;
            if (_page.Preferences.MinimizeToTray && CanHide) HideToTray();
            else _ = ExitAsync();
        };
        AppWindow.Changed += (_, e) =>
        {
            if (e.DidPresenterChange && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized }
                && _page.Preferences.MinimizeToTray && CanHide) HideToTray();
        };
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated) _page.RefreshAccess();
        };
        Closed += (_, _) => _desktop?.Dispose();
    }

    public bool CanHide => _desktop?.IsTrayAvailable == true;

    public async Task InitializeAsync(bool background)
    {
        if (!background || !CanHide) ShowMain();
        else { _hidden = true; AppWindow.Hide(); }
        await _page.InitializeAsync();
    }

    public void ShowMain()
    {
        _hidden = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.Restore();
        AppWindow.Show();
        Activate();
        _page.RefreshAccess();
    }

    public void HideToTray()
    {
        if (!CanHide) { _page.ReportStatus("托盘暂不可用，窗口已保留。"); return; }
        if (_hidden) return;
        _hidden = true;
        AppWindow.Hide();
    }

    public void ShowCode(string code)
    {
        if (_page.Preferences.ShowPopup) _desktop?.ShowCode(code);
    }

    /// <summary>仅预览浮窗外观：不复制、不填入。</summary>
    public void ShowPreview(string code) => _desktop?.ShowCode(code, preview: true);

    public void DismissPopup() => _desktop?.DismissPopup();

    public async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        _desktop?.DismissPopup();
        await _page.ShutdownAsync();
        _desktop?.Dispose();
        Close();
    }
}

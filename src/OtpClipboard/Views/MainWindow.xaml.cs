using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using OtpClipboard.Desktop;

namespace OtpClipboard.Views;

/// <summary>按需创建的主窗口；关闭后由 App 退出当前 UI 进程。</summary>
public sealed partial class MainWindow : Window
{
    private readonly AppController _controller;

    public MainWindow(AppController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _controller = controller;
        InitializeComponent();

        double scale = DesktopNativeMethods.GetDpiForWindow(
            WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var work = DisplayArea.GetFromWindowId(
            AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Math.Min((int)(760 * scale), work.Width - 40),
            Math.Min((int)(720 * scale), work.Height - 40)));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        RootFrame.Navigate(typeof(MainPage), _controller);
        Closed += (_, _) => RootFrame.Content = null;
    }
}

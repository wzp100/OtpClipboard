using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using OtpClipboard.Views;

namespace OtpClipboard;

public partial class App : Application
{
    private MainWindow? _window;
    private AppInstance? _instance;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        _instance = AppInstance.FindOrRegisterForKey("OtpClipboard.Main");
        if (!_instance.IsCurrent)
        {
            await _instance.RedirectActivationToAsync(activation);
            Exit();
            return;
        }

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _instance.Activated += (_, redirected) =>
        {
            if (redirected.Kind != ExtendedActivationKind.StartupTask)
                dispatcher.TryEnqueue(() => _window?.ShowMain());
        };
        bool background = activation.Kind == ExtendedActivationKind.StartupTask;
        _window = new MainWindow();
        await _window.InitializeAsync(background);
    }
}

namespace OtpClipboard.Api;

internal static class BridgeNames
{
    public const string Mutex = @"Local\OtpClipboard.Singleton.v1";
    public const string AppInstance = "OtpClipboard.Ui.v1";
    public const string Pipe = "OtpClipboard.Control.v1";

    public const string Snapshot = "snapshot";
    public const string Show = "show";
    public const string Exit = "exit";
    public const string RequestAccess = "request-access";
    public const string ToggleMonitoring = "toggle-monitoring";
    public const string Scan = "scan";
    public const string UpdatePreferences = "update-preferences";
    public const string SetStartup = "set-startup";
    public const string GetStartup = "get-startup";
    public const string Recopy = "recopy";
    public const string ClearRecords = "clear-records";
    public const string PreviewPopup = "preview-popup";
    public const string DismissPopup = "dismiss-popup";
}

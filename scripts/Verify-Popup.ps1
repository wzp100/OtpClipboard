param(
    [int]$ProcessId = 0,
    [string]$OutputPath = "popup-new.png",
    # Extra pixels captured around the popup so its shadow and rounded corners are visible.
    [int]$Margin = 24
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();' -Name DpiHelper -Namespace Win32
[void][Win32.DpiHelper]::SetProcessDPIAware()
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class WindowHelper
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxLength);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, System.UIntPtr extraInfo);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(System.IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(System.IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr after, int x, int y, int cx, int cy, uint flags);

    public struct WinInfo
    {
        public System.IntPtr Handle;
        public string Class;
        public string Title;
        public int X, Y, W, H;
        public bool Visible;
    }

    [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(System.IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(System.IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(System.IntPtr hWnd, StringBuilder text, int maxLength);

    public static System.Collections.Generic.List<WinInfo> ListWindows(int processId)
    {
        var list = new System.Collections.Generic.List<WinInfo>();
        EnumWindows((h, s) =>
        {
            uint owner;
            GetWindowThreadProcessId(h, out owner);
            if (owner != (uint)processId) return true;
            var cls = new StringBuilder(256);
            GetClassName(h, cls, 256);
            var t = new StringBuilder(256);
            GetWindowText(h, t, 256);
            RECT r;
            GetWindowRect(h, out r);
            list.Add(new WinInfo
            {
                Handle = h,
                Class = cls.ToString(),
                Title = t.ToString(),
                X = r.Left,
                Y = r.Top,
                W = r.Right - r.Left,
                H = r.Bottom - r.Top,
                Visible = IsWindowVisible(h)
            });
            return true;
        }, System.IntPtr.Zero);
        return list;
    }

    public static IntPtr FindHost(int processId)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hWnd, state) =>
        {
            uint owner;
            GetWindowThreadProcessId(hWnd, out owner);
            if (owner != (uint)processId) return true;
            var text = new StringBuilder(256);
            GetWindowText(hWnd, text, 256);
            if (text.ToString().StartsWith("OtpClipboard"))
            {
                result = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@

if ($ProcessId -eq 0) {
    $process = Get-Process -Name "OtpClipboard" -ErrorAction Stop | Select-Object -First 1
    if (-not $process) { throw "OtpClipboard process was not found." }
    $ProcessId = $process.Id
}

$root = [System.Windows.Automation.AutomationElement]::RootElement
$processCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
function Find-MainWindow {
    $windowCondition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Window)))
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCondition)
}
$main = Find-MainWindow
if (-not $main -or $main.Current.IsOffscreen) {
    $hostWindow = [WindowHelper]::FindHost($ProcessId)
    if ($hostWindow -eq [IntPtr]::Zero) { throw "Desktop host was not found for PID $ProcessId." }
    [void][WindowHelper]::PostMessage($hostWindow, 0x312, [IntPtr]1, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 650
    $main = Find-MainWindow
}
if (-not $main) { throw "Main window was not found for PID $ProcessId." }

$hwnd = [IntPtr]$main.Current.NativeWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { $hwnd = (Get-Process -Id $ProcessId).MainWindowHandle }
if ($hwnd -ne [IntPtr]::Zero) {
    [void][WindowHelper]::ShowWindow($hwnd, 9)
    [void][WindowHelper]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x53)
    [void][WindowHelper]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x53)
    [void][WindowHelper]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 400
}

# The preview button lives on the Settings tab; collapsed tabs are absent from the UIA tree, so select it first.
$settingsName = -join @([char]0x8BBE, [char]0x7F6E)
$tabs = $main.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $settingsName)))
foreach ($tab in $tabs) {
    $selectPattern = $null
    if ($tab.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selectPattern)) {
        $selectPattern.Select()
        Start-Sleep -Milliseconds 600
        break
    }
}

$buttons = $main.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)))
$previewButton = $null
$buttonNames = @()
foreach ($button in $buttons) {
    $buttonNames += $button.Current.Name
    if ($button.Current.Name -like "*246810*") { $previewButton = $button; break }
}
if (-not $previewButton) {
    throw ("Preview button was not found. Buttons: " + ($buttonNames -join " | "))
}
$invokePattern = $null
if (-not $previewButton.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
    throw "Preview button does not support InvokePattern."
}
$invokePattern.Invoke()
Start-Sleep -Milliseconds 450

$wins = [WindowHelper]::ListWindows($ProcessId)
$popupWin = $wins | Where-Object { $_.Visible -and $_.Class.StartsWith("OtpClipboard.Desktop.") -and $_.W -gt 10 -and $_.W -le 900 -and $_.H -le 250 } | Select-Object -First 1
$rect = $null
if ($popupWin) { $rect = New-Object System.Drawing.Rectangle(($popupWin.X - $Margin), ($popupWin.Y - $Margin), ($popupWin.W + 2 * $Margin), ($popupWin.H + 2 * $Margin)) }
else {
    $dump = $wins | ForEach-Object { ("v={0} c={1} r={2}x{3}@{4},{5}" -f $_.Visible, $_.Class, $_.W, $_.H, $_.X, $_.Y) }
    throw ("Popup window was not found. Windows: " + ($dump -join " | "))
}

$result = [ordered]@{
    ProcessId = $ProcessId
    Name = $popupWin.Title
    ClassName = $popupWin.Class
    Left = [int]$popupWin.X
    Top = [int]$popupWin.Y
    Width = [int]$popupWin.W
    Height = [int]$popupWin.H
}

$bitmap = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, $bitmap.Size)
$graphics.Dispose()
$bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

$result | ConvertTo-Json -Compress

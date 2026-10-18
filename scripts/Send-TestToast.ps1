param(
    # Diagnostic helper: shows a real Windows toast so the clipboard utility can be
    # tested end to end without waiting for a real phone message.
    #
    # Examples:
    #   powershell -ExecutionPolicy Bypass -File .\scripts\Send-TestToast.ps1
    #   powershell -ExecutionPolicy Bypass -File .\scripts\Send-TestToast.ps1 -Chinese -Code 135790
    #   powershell -ExecutionPolicy Bypass -File .\scripts\Send-TestToast.ps1 -AppUserModelId 'Microsoft.WindowsStore_8wekyb3d8bbwe!App'
    #
    # Keep this file ASCII-only so Windows PowerShell 5.1 can read it on any system.
    [string]$AppUserModelId = 'Microsoft.YourPhone_8wekyb3d8bbwe!App',
    [string]$Title = 'Phone Link',
    [string]$Code = '246810',
    [string]$Body = '',
    [switch]$Chinese,
    [switch]$KeepInActionCenter
)

$ErrorActionPreference = 'Stop'

[void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
[void][Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]

if (-not $Body -and $env:OTP_TEST_BODY) { $Body = $env:OTP_TEST_BODY }
if ($env:OTP_TEST_TITLE) { $Title = $env:OTP_TEST_TITLE }

if ($Body) {
    # Caller supplied the whole message, for example through OTP_TEST_BODY to keep Chinese text intact.
}
elseif ($Chinese) {
    # U+3010 U+624B U+673A U+8FDE U+63A5 U+3011 and U+60A8 U+7684 U+9A8C U+8BC1 U+7801 U+662F
    $bracket = -join ([char]0x3010, [char]0x624B, [char]0x673A, [char]0x8FDE, [char]0x63A5, [char]0x3011)
    $Title = $bracket
    $Body = $bracket + (-join ([char]0x60A8, [char]0x7684, [char]0x9A8C, [char]0x8BC1, [char]0x7801, [char]0x662F)) + " $Code"
}
else {
    $Body = "Your verification code is $Code. Do not share it."
}

$payload = '<toast><visual><binding template="ToastGeneric"><text>{0}</text><text>{1}</text></binding></visual></toast>' -f $Title, $Body
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($payload)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($AppUserModelId).Show($toast)

Write-Output "SHOWN aumid=$AppUserModelId"
Write-Output "TEXT $Body"

if (-not $KeepInActionCenter) {
    Start-Sleep -Seconds 1
    try {
        [Windows.UI.Notifications.ToastNotificationManager]::History.Clear($AppUserModelId)
        Write-Output 'CLEARED from the action center'
    }
    catch {
        Write-Warning "Could not clear the notification history: $($_.Exception.Message)"
    }
}

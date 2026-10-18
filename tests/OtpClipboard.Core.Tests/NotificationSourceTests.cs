namespace OtpClipboard.Core.Tests;

public class NotificationSourceTests
{
    private const string PhoneLink = NotificationSource.PhoneLinkPackageFamilyName;

    [Theory]
    [InlineData(PhoneLink, "", true)]
    [InlineData("microsoft.yourphone_8wekyb3d8bbwe", "", true)]
    [InlineData("", PhoneLink + "!App", true)]
    [InlineData(null, PhoneLink + "!App", true)]
    [InlineData("Microsoft.WindowsStore_8wekyb3d8bbwe", "Microsoft.WindowsStore_8wekyb3d8bbwe!App", false)]
    [InlineData("", PhoneLink, false)] // AUMID 必须带 “!应用 ID”。
    [InlineData(null, null, false)]
    public void IsPhoneLink(string? packageFamily, string? appUserModelId, bool expected) =>
        Assert.Equal(expected, NotificationSource.IsPhoneLink(packageFamily, appUserModelId));

    [Theory]
    [InlineData("手机连接", PhoneLink, "手机连接")]
    [InlineData("Microsoft Store", "Microsoft.WindowsStore_8wekyb3d8bbwe", "Microsoft Store（Microsoft.WindowsStore_8wekyb3d8bbwe）")]
    [InlineData("", "Microsoft.WindowsStore_8wekyb3d8bbwe", "Microsoft.WindowsStore_8wekyb3d8bbwe")]
    [InlineData("记事本", "", "记事本")]
    [InlineData("", "", "未知应用")]
    [InlineData(null, null, "未知应用")]
    public void Describe(string? appName, string? packageFamily, string expected) =>
        Assert.Equal(expected, NotificationSource.Describe(appName, packageFamily));
}

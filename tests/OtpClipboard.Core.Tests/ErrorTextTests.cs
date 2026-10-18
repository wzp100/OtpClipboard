using System.Runtime.InteropServices;

namespace OtpClipboard.Core.Tests;

public class ErrorTextTests
{
    [Fact]
    public void Describe_ClipboardBusyHasFriendlyText() =>
        Assert.Equal("剪贴板被其他程序短暂占用（COMException 0x800401D0）",
            ErrorText.Describe(new COMException("", ErrorText.ClipboardBusyHResult)));

    [Fact]
    public void Describe_IncludesTypeHResultAndMessage()
    {
        var ex = new InvalidOperationException("出错了");
        Assert.Equal($"InvalidOperationException 0x{ex.HResult:X8}：出错了", ErrorText.Describe(ex));
    }

    [Fact]
    public void Describe_EmptyMessageHasPlaceholder() =>
        Assert.EndsWith("（系统未提供错误描述）", ErrorText.Describe(new COMException(" ", unchecked((int)0x80004005))));

    [Fact]
    public void Describe_NullThrows() => Assert.Throws<ArgumentNullException>(() => ErrorText.Describe(null!));
}

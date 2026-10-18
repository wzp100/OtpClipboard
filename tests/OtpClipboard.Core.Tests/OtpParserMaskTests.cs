namespace OtpClipboard.Core.Tests;

public class OtpParserMaskTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("1", "•")]
    [InlineData("12", "••")]
    [InlineData("123", "•••")]
    [InlineData("1234", "••••")]
    [InlineData("12345", "12•45")]
    [InlineData("123456", "12••56")]
    [InlineData("1234567", "12•••67")]
    [InlineData("12345678", "12••••78")]
    public void Mask_KeepsLengthAndEdges(string? code, string expected)
    {
        string masked = OtpParser.Mask(code!);
        Assert.Equal(expected, masked);
        Assert.Equal(code?.Length ?? 0, masked.Length);
    }
}

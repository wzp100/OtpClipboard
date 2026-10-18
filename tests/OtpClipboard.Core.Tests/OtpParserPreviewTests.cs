namespace OtpClipboard.Core.Tests;

public class OtpParserPreviewTests
{
    private const int Max = OtpParser.MaxInputLength;
    private static string Filler(int count) => new('甲', count);

    public static TheoryData<string, string, int, string> Cases => new()
    {
        { "合并空白并掩码", " \r\n验证码:\t123456\n请勿泄露  ", 120, "验证码: 12••56 请勿泄露" },
        { "四位与八位", "1234 12345678", 120, "•••• 12••••78" },
        { "兼容原长号码分组规则", "1234567890123456", 120, "12••••7890••••56" },
        { "无关键词也掩码", "订单 123456", 120, "订单 12••56" },
        { "不遮挡不足四位的数字", "123", 120, "123" },
        { "空字符串", "", 120, "" },
        { "空白", " \t\r\n", 120, "" },
        { "空引用", null!, 120, "" },
        { "零长度", "验证码 123456", 0, "" },
        { "长度一截断", "文字", 1, "…" },
        { "长度一无需截断", "字", 1, "字" },
        { "恰好上限", "文字", 2, "文字" },
        { "超限含省略号", "一二三四五六", 5, "一二三四…" },
        { "先掩码再截断", "123456", 4, "12•…" },
        { "默认长度边界", Filler(120), 120, Filler(120) },
        { "默认长度超界", Filler(121), 120, Filler(119) + "…" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Preview(string name, string body, int maxLength, string expected)
    {
        _ = name;
        Assert.Equal(expected, OtpParser.Preview(body, maxLength));
    }

    [Fact]
    public void Preview_DefaultMaxLengthIs120() =>
        Assert.Equal(Filler(119) + "…", OtpParser.Preview(Filler(121)));

    [Fact]
    public void Preview_OverlongInputIsTruncated() =>
        Assert.Equal(Filler(119) + "…", OtpParser.Preview(Filler(Max + 1), 120));

    [Fact]
    public void Preview_MarksInputTruncationExplicitly() =>
        Assert.Equal(Filler(Max) + "…", OtpParser.Preview(Filler(Max + 1), int.MaxValue));

    [Fact]
    public void Preview_InputTruncationEqualToOutputLimit() =>
        Assert.Equal(Filler(Max - 1) + "…", OtpParser.Preview(Filler(Max + 1), Max));

    [Fact]
    public void Preview_NegativeMaxLengthThrows()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => OtpParser.Preview("验证码 123456", -1));
        Assert.Equal("maxLength", ex.ParamName);
    }
}

using System.Globalization;

namespace OtpClipboard.Core.Tests;

public class OtpParserExtractTests
{
    private const int Max = OtpParser.MaxInputLength;
    private static string Filler(int count) => new('甲', count);

    // 第一列是用例说明，只用于在测试结果中区分用例。
    public static TheoryData<string, string, string?> Cases => new()
    {
        // README 中的示例（号码已用省略号缩写，数字为虚构）。
        { "README RCS 示例", "1065…000001@botplatform.rcs.chinamobile.com 【示例】验证码:802413", "802413" },
        // scripts/Send-TestToast.ps1 实际生成的中英文通知正文。
        { "测试通知英文示例", "Your verification code is 246810. Do not share it.", "246810" },
        { "测试通知中文示例", "【手机连接】您的验证码是 135790", "135790" },
        { "后置示例", "验证码 123456", "123456" },
        { "前置示例", "123456 是您的验证码", "123456" },
        { "连续长发送方号码回归", "1065900000000000001@botplatform.rcs.chinamobile.com 【示例】验证码:802413", "802413" },
        { "发送方紧邻关键词", "1065900000000000001 验证码:802413", "802413" },
        { "仅发送方长号码", "1065900000000000001 验证码", null },
        { "关键词后仅长号码", "验证码:1065900000000000001", null },
        { "后置四位下界", "验证码:1234", "1234" },
        { "后置八位上界", "验证码:12345678", "12345678" },
        { "前置四位下界", "1234 是您的验证码", "1234" },
        { "前置八位上界", "12345678 是您的验证码", "12345678" },
        { "保留前导零", "OTP 000012", "000012" },
        { "三位不足", "验证码 123", null },
        { "后置九位超长", "验证码 123456789", null },
        { "前置九位超长", "123456789 是您的验证码", null },
        { "无关键词的数字", "订单号 123456", null },
        { "仅数字", "123456", null },
        { "仅关键词", "验证码", null },
        { "空字符串", "", null },
        { "空白字符串", " \r\n\t", null },
        { "空引用防御", null!, null },
        { "后置优先于同处前置", "123456 验证码 654321", "654321" },
        { "全文后置优先", "123456 是您的验证码。" + Filler(25) + "OTP:876543", "876543" },
        { "后置不匹配时回退", "123456 验证码 123456789", "123456" },
        { "后置首个匹配", "OTP 1111，验证码 2222", "1111" },
        { "前置首个匹配", "1111验证码" + Filler(25) + "2222验证码", "1111" },
        { "后置间距24", "验证码" + Filler(24) + "1234", "1234" },
        { "后置间距25", "验证码" + Filler(25) + "1234", null },
        { "前置间距12", "1234" + Filler(12) + "验证码", "1234" },
        { "前置间距13", "1234" + Filler(13) + "验证码", null },
        { "不能跨越其他数字", "验证码 12 后面是 123456", null },
        { "不能跨越其他数字回退", "123456 和 12 验证码", null },
        { "多行通知", "OTP\r\n123456", "123456" },
        { "全角数字不接受", "验证码 １２３４５６", null },
        { "阿拉伯文数字不接受", "OTP ١٢٣٤٥٦", null },
        { "混合数字不截取后半段", "验证码 １123456", null },
        { "混合数字不截取前半段", "验证码 123456１", null },
        { "前置混合数字左边界", "１123456 验证码", null },
        { "前置混合数字右边界", "123456１ 验证码", null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Extract(string name, string body, string? expected)
    {
        _ = name;
        Assert.Equal(expected, OtpParser.Extract(body));
    }

    public static TheoryData<string> Keywords =>
    [
        "验证码", "校验码", "动态码", "一次性密码", "一次性代码", "安全码", "认证码", "短信码",
        "verification", "verification code", "VERIFY", "OTP", "one-time", "one time", "onetime",
        "one-time password", "one time password", "passcode", "security code", "SeCuRiTy\tCoDe",
    ];

    [Theory]
    [MemberData(nameof(Keywords))]
    public void Extract_CodeAfterKeyword(string keyword) =>
        Assert.Equal("123456", OtpParser.Extract($"{keyword}:123456"));

    [Theory]
    [MemberData(nameof(Keywords))]
    public void Extract_CodeBeforeKeyword(string keyword) =>
        Assert.Equal("123456", OtpParser.Extract($"123456 {keyword}"));

    // 超长输入单独写成 Fact，避免把几十万字符的用例数据交给测试资源管理器序列化。
    [Fact]
    public void Extract_AcceptsInputAtLengthLimit() =>
        Assert.Equal("1234", OtpParser.Extract(Filler(Max - 8) + "验证码:1234"));

    [Fact]
    public void Extract_LimitLengthWithoutKeyword() => Assert.Null(OtpParser.Extract(Filler(Max)));

    [Fact]
    public void Extract_RejectsOverlongInputEvenWithLeadingCode() =>
        Assert.Null(OtpParser.Extract("OTP 1234" + Filler(Max)));

    [Fact]
    public void Extract_RejectsOverlongInputWithTrailingCode() =>
        Assert.Null(OtpParser.Extract(Filler(Max) + "OTP 1234"));

    [Fact]
    public void Extract_DoesNotTruncateLongNumberIntoCode() =>
        Assert.Null(OtpParser.Extract(Filler(Max - 8) + "OTP 123456789"));

    [Fact]
    public void Extract_RejectsHugeInputQuickly() => Assert.Null(OtpParser.Extract(new string('9', 1_000_000)));

    [Fact]
    public void Extract_ManyKeywordsWithoutCode() =>
        Assert.Null(OtpParser.Extract(string.Concat(Enumerable.Repeat("verification ", 1_200))));

    [Fact]
    public void Extract_IgnoresTurkishCasingRules()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("123456", OtpParser.Extract("VERIFICATION CODE:123456"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}

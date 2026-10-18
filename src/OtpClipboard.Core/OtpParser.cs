using System;
using System.Text.RegularExpressions;

namespace OtpClipboard.Core;

/// <summary>不依赖通知 API 或界面的验证码提取和摘要工具。</summary>
public static class OtpParser
{
    /// <summary>正则最多处理的 UTF-16 字符数；提取时拒绝超长输入，避免截断产生假验证码。</summary>
    public const int MaxInputLength = 16_384;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private const string KeywordPattern =
        @"(?:验证码|校验码|动态码|一次性(?:密码|代码)|安全码|认证码|短信码|verification(?:\s+code)?|verify|otp|one[-\s]?time(?:\s+password)?|passcode|security\s+code)";

    // 只捕获 ASCII 数字；边界使用 Unicode \d，防止从混合数字或发送方长号码中截取一段。
    private static readonly Regex AfterKeywordRegex = new(
        $@"{KeywordPattern}\D{{0,24}}(?<!\d)(?<code>[0-9]{{4,8}})(?!\d)",
        Options | RegexOptions.IgnoreCase, MatchTimeout);
    private static readonly Regex BeforeKeywordRegex = new(
        $@"(?<!\d)(?<code>[0-9]{{4,8}})(?!\d)\D{{0,12}}{KeywordPattern}",
        Options | RegexOptions.IgnoreCase, MatchTimeout);

    // 保留原摘要规则：连续数字按 4–8 位分组遮挡，长号码也参与遮挡。
    private static readonly Regex DigitRunRegex = new(@"[0-9]{4,8}", Options, MatchTimeout);
    private static readonly Regex WhitespaceRegex = new(@"\s+", Options, MatchTimeout);

    /// <summary>先在全文找关键词后的验证码，再回退查找关键词前的验证码；空、超长或超时返回 null。</summary>
    public static string? Extract(string body)
    {
        if (string.IsNullOrEmpty(body) || body.Length > MaxInputLength) return null;

        try
        {
            Match match = AfterKeywordRegex.Match(body);
            if (!match.Success) match = BeforeKeywordRegex.Match(body);
            return match.Success ? match.Groups["code"].Value : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>
    /// 合并空白、遮挡数字，再截断；maxLength 包括省略号，0 返回空，负数抛出异常。
    /// 最多读取输入前 MaxInputLength 个字符；发生正则超时时返回空，避免泄露未遮挡的正文。
    /// </summary>
    public static string Preview(string body, int maxLength = 120)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (maxLength == 0 || string.IsNullOrEmpty(body)) return string.Empty;

        bool inputTruncated = body.Length > MaxInputLength;
        string bounded = inputTruncated ? body[..MaxInputLength] : body;
        try
        {
            string flattened = WhitespaceRegex.Replace(bounded, " ").Trim();
            string masked = DigitRunRegex.Replace(flattened, match => Mask(match.Value));
            if (masked.Length <= maxLength && !inputTruncated) return masked;

            int take = Math.Min(masked.Length, maxLength - 1);
            return string.Concat(masked.AsSpan(0, take), "…");
        }
        catch (RegexMatchTimeoutException)
        {
            return string.Empty;
        }
    }

    /// <summary>不超过四位时全部遮挡，否则保留首尾各两位；结果与输入等长，空输入返回空。</summary>
    public static string Mask(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        return code.Length <= 4
            ? new string('•', code.Length)
            : $"{code[..2]}{new string('•', code.Length - 4)}{code[^2..]}";
    }
}

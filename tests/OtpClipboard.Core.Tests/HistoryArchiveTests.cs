using System.Globalization;
using System.Text.Json;

namespace OtpClipboard.Core.Tests;

public class HistoryArchiveTests
{
    [Fact]
    public void LegacyHistory_DropsNotificationContentAndCode()
    {
        const string legacy = """
            [{"Status":"已复制","Kind":"Copied","Title":"私密发送人",
              "Detail":"示例短信验证码 246810","Code":"246810","Time":"10-18 12:34:56"}]
            """;
        var restored = HistoryArchive.Deserialize(legacy);
        Assert.Single(restored);
        string saved = HistoryArchive.Serialize(restored);
        using var document = JsonDocument.Parse(saved);
        Assert.Equal(new[] { "Kind", "Time" },
            document.RootElement[0].EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("246810", saved);
        Assert.DoesNotContain("私密发送人", saved);
    }

    [Fact]
    public void Metadata_CannotSmugglePrivateTextIntoAllowedFields()
    {
        var entries = new[]
        {
            new HistoryEntry("OTP 246810", "10-18 12:34:56"),
            new HistoryEntry("Copied", "私密短信 246810"),
            new HistoryEntry("Failed", "10-18 12:34:56"),
        };
        var restored = HistoryArchive.Deserialize(HistoryArchive.Serialize(entries));
        Assert.Equal(new[] { entries[2] }, restored);
    }

    [Fact]
    public void Deserialize_IgnoresNullAndIncompleteLegacyEntries()
    {
        Assert.Empty(HistoryArchive.Deserialize("[null,{}, {\"Kind\":\"Copied\"}]"));
        Assert.Empty(HistoryArchive.Deserialize("null"));
    }

    [Fact]
    public void History_IsBoundedAndKeepsNewestFirst()
    {
        var entries = Enumerable.Range(0, 250)
            .Select(index => new HistoryEntry("Info", DateTime.Today.AddSeconds(index).ToString(HistoryArchive.TimeFormat, CultureInfo.InvariantCulture)))
            .ToArray();
        var restored = HistoryArchive.Deserialize(HistoryArchive.Serialize(entries));
        Assert.Equal(entries.Take(HistoryArchive.MaxRecords), restored);
    }

    [Fact]
    public void LeapDay_DoesNotDependOnCurrentYear() =>
        Assert.Single(HistoryArchive.Deserialize("[{\"Kind\":\"Info\",\"Time\":\"02-29 12:34:56\"}]"));

    [Fact]
    public void CorruptHistory_ReportsFailure() =>
        Assert.Throws<JsonException>(() => HistoryArchive.Deserialize("[broken"));
}

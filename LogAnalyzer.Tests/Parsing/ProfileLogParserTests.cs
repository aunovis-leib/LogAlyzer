using LogAnalyzer.Models;
using LogAnalyzer.Services.Parsing;
using Xunit;

namespace LogAnalyzer.Tests.Parsing;

public class ProfileLogParserTests
{
    [Fact]
    public void TryParse_ParsesLineWithMultiCharacterSeparator()
    {
        var parser = new ProfileLogParser(new ParserProfile
        {
            Name = "custom",
            DateFormat = "dd.MM.yyyy HH:mm:ss.fff",
            Splitter = "||"
        });

        var ok = parser.TryParse("01.01.2025 10:00:00.000||Warning||Hello||World", out var entry);

        Assert.True(ok);
        Assert.Equal(LogType.Warning, entry.Type);
        Assert.Equal("Hello||World", entry.Text);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForInvalidLine()
    {
        var parser = new ProfileLogParser(new ParserProfile
        {
            Name = "custom",
            DateFormat = "dd.MM.yyyy HH:mm:ss.fff",
            Splitter = "||"
        });

        var ok = parser.TryParse("nope", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParse_ParsesLineWithTimeOnlyTimestamp()
    {
        var parser = new ProfileLogParser(new ParserProfile
        {
            Name = "custom",
            DateFormat = "dd.MM.yyyy HH:mm:ss.fff",
            Splitter = "|"
        });

        var ok = parser.TryParse("05:33:31.255Z|4|0FAC* ==> UaCoreServerApplication::start", out var entry);

        Assert.True(ok);
        Assert.Equal(DateTime.Today.AddHours(5).AddMinutes(33).AddSeconds(31).AddMilliseconds(255), entry.Date);
        Assert.Equal(LogType.Info, entry.Type);
        Assert.Equal("0FAC* ==> UaCoreServerApplication::start", entry.Text);
    }
}

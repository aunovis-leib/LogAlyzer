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
}

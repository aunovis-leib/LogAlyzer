using System.Linq;
using LogAnalyzer.Models;
using LogAnalyzer.Services.Parsing;
using Xunit;

namespace LogAnalyzer.Tests.Parsing;

public class CsvLogParserTests
{
    private static CsvLogParser CreateParser(ParserProfile? profile = null)
        => new(profile ?? new ParserProfile { Name = "csv", IsCsv = true });

    [Fact]
    public void TryParse_HeaderRow_ReturnsFalseAndParsesHeaders()
    {
        var parser = CreateParser();

        var ok = parser.TryParse("Timestamp,Level,Message", out _);

        Assert.False(ok);
        Assert.True(parser.HeaderParsed);
    }

    [Fact]
    public void TryParse_DataRow_PopulatesFieldsByHeaderName()
    {
        var parser = CreateParser();
        parser.TryParse("Timestamp,Level,Message", out _);

        var ok = parser.TryParse("01.01.2025 10:00:00.000,Warning,Hello", out var entry);

        Assert.True(ok);
        Assert.Equal("Warning", entry.Fields["Level"]);
        Assert.Equal("Hello", entry.Fields["Message"]);
    }

    [Fact]
    public void TryParse_AutoDetectsDateColumn_AndSetsEntryDate()
    {
        var parser = CreateParser();
        parser.TryParse("Timestamp,Level,Message", out _);

        parser.TryParse("01.01.2025 10:00:00.000,Info,Started", out var entry);

        Assert.Equal(new DateTime(2025, 1, 1, 10, 0, 0), entry.Date);
    }

    [Fact]
    public void DisplayColumns_ExcludesDateColumn()
    {
        var parser = CreateParser();
        parser.TryParse("Timestamp,Level,Message", out _);

        Assert.DoesNotContain("Timestamp", parser.DisplayColumns);
        Assert.Contains("Level", parser.DisplayColumns);
        Assert.Contains("Message", parser.DisplayColumns);
    }

    [Fact]
    public void TryParse_UsesConfiguredDateColumn()
    {
        var profile = new ParserProfile
        {
            Name = "csv",
            IsCsv = true,
            CsvDateColumn = "When"
        };
        var parser = CreateParser(profile);
        parser.TryParse("Id,When,Message", out _);

        parser.TryParse("1,01.01.2025 10:00:00.000,Started", out var entry);

        Assert.Equal(new DateTime(2025, 1, 1, 10, 0, 0), entry.Date);
        Assert.DoesNotContain("When", parser.DisplayColumns);
    }

    [Fact]
    public void DisplayColumns_HonorsConfiguredCsvColumns()
    {
        var profile = new ParserProfile
        {
            Name = "csv",
            IsCsv = true,
            CsvColumns = { "Level", "Message" }
        };
        var parser = CreateParser(profile);
        parser.TryParse("Timestamp,Level,Message,Extra", out _);

        Assert.Equal(new[] { "Level", "Message" }, parser.DisplayColumns.ToArray());
    }

    [Fact]
    public void TryParse_UsesCustomDelimiter()
    {
        var profile = new ParserProfile
        {
            Name = "csv",
            IsCsv = true,
            CsvDelimiter = ";"
        };
        var parser = CreateParser(profile);
        parser.TryParse("Timestamp;Level;Message", out _);

        var ok = parser.TryParse("01.01.2025 10:00:00.000;Error;Boom", out var entry);

        Assert.True(ok);
        Assert.Equal("Error", entry.Fields["Level"]);
        Assert.Equal("Boom", entry.Fields["Message"]);
    }

    [Fact]
    public void TryParse_HandlesQuotedFieldsWithDelimiterAndEscapedQuotes()
    {
        var parser = CreateParser();
        parser.TryParse("Timestamp,Level,Message", out _);

        var ok = parser.TryParse("01.01.2025 10:00:00.000,Info,\"Hello, \"\"World\"\"\"", out var entry);

        Assert.True(ok);
        Assert.Equal("Hello, \"World\"", entry.Fields["Message"]);
    }

    [Fact]
    public void TryParse_MissingTrailingColumns_YieldsEmptyStrings()
    {
        var parser = CreateParser();
        parser.TryParse("Timestamp,Level,Message", out _);

        var ok = parser.TryParse("01.01.2025 10:00:00.000,Info", out var entry);

        Assert.True(ok);
        Assert.Equal(string.Empty, entry.Fields["Message"]);
    }

    [Fact]
    public void TryParse_AppliesTimeOffset()
    {
        var profile = new ParserProfile
        {
            Name = "csv",
            IsCsv = true,
            TimeOffsetHours = 2
        };
        var parser = CreateParser(profile);
        parser.TryParse("Timestamp,Level,Message", out _);

        parser.TryParse("01.01.2025 10:00:00.000,Info,Started", out var entry);

        Assert.Equal(new DateTime(2025, 1, 1, 12, 0, 0), entry.Date);
    }

    [Fact]
    public void TryParse_EmptyLine_ReturnsFalse()
    {
        var parser = CreateParser();

        var ok = parser.TryParse(string.Empty, out _);

        Assert.False(ok);
        Assert.False(parser.HeaderParsed);
    }

    [Fact]
    public void TryParse_NoDateColumn_LeavesDefaultDate()
    {
        var parser = CreateParser();
        parser.TryParse("Id,Level,Message", out _);

        var ok = parser.TryParse("1,Info,Started", out var entry);

        Assert.True(ok);
        Assert.Equal(default, entry.Date);
    }
}

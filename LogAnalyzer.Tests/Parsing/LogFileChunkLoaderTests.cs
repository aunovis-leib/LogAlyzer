using LogAnalyzer.Models;
using LogAnalyzer.Services.Parsing;
using Xunit;

namespace LogAnalyzer.Tests.Parsing;

public class LogFileChunkLoaderTests
{
    [Fact]
    public async Task LoadAsync_SplitsIntoChunks_AndKeepsDetailLines()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile,
            [
                "01.01.2025 10:00:00.000|Info|Start",
                "detail-1",
                "01.01.2025 10:01:00.000|Error|Boom",
                "detail-2",
                "detail-3",
                "01.01.2025 10:02:00.000|Debug|Done"
            ]);

            var loader = new LogFileChunkLoader(new LegacyLogParser());
            var chunks = new List<LogLoadChunk>();

            await foreach (var chunk in loader.LoadAsync([tempFile], 2))
            {
                chunks.Add(chunk);
            }

            Assert.Equal(2, chunks.Count);
            Assert.Equal(2, chunks[0].Entries.Count);
            Assert.Single(chunks[1].Entries);

            var first = chunks[0].Entries[0];
            Assert.Equal(LogType.Info, first.Type);
            Assert.Single(first.Detail);
            Assert.Equal("detail-1", first.Detail[0]);

            var second = chunks[0].Entries[1];
            Assert.Equal(LogType.Error, second.Type);
            Assert.Equal(2, second.Detail.Length);

            var third = chunks[1].Entries[0];
            Assert.Equal(LogType.Debug, third.Type);
            Assert.Empty(third.Detail);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_ThrowsOnCancellation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var lines = Enumerable.Range(0, 5000)
                .Select(i => $"01.01.2025 10:00:{i % 60:00}.000|Info|Row-{i}")
                .ToArray();
            await File.WriteAllLinesAsync(tempFile, lines);

            var loader = new LogFileChunkLoader(new LegacyLogParser());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in loader.LoadAsync([tempFile], 256, cts.Token))
                {
                }
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_TwoFilesWithDifferentDates_AllEntriesKeepCorrectDate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "TwoFilesDifferentDates_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        var file1 = Path.Combine(tempDir, "first.log");
        var file2 = Path.Combine(tempDir, "second.log");

        try
        {
            await File.WriteAllLinesAsync(file1,
            [
                "22.04.2026 10:00:00.000|Info|first-1",
                "22.04.2026 10:01:00.000|Warning|first-2"
            ]);

            await File.WriteAllLinesAsync(file2,
            [
                "23.04.2026 11:00:00.000|Error|second-1",
                "23.04.2026 11:01:00.000|Debug|second-2"
            ]);

            var loader = new LogFileChunkLoader(new LegacyLogParser());
            var entries = new List<LogFileEntry>();

            await foreach (var chunk in loader.LoadAsync([file1, file2], 10))
            {
                entries.AddRange(chunk.Entries);
            }

            Assert.Equal(4, entries.Count);

            Assert.Equal(new DateTime(2026, 4, 22, 10, 0, 0, 0), entries[0].Date);
            Assert.Equal(new DateTime(2026, 4, 22, 10, 1, 0, 0), entries[1].Date);
            Assert.Equal(new DateTime(2026, 4, 23, 11, 0, 0, 0), entries[2].Date);
            Assert.Equal(new DateTime(2026, 4, 23, 11, 1, 0, 0), entries[3].Date);

            Assert.All(entries.Take(2), e => Assert.Equal(new DateTime(2026, 4, 22), e.Date.Date));
            Assert.All(entries.Skip(2), e => Assert.Equal(new DateTime(2026, 4, 23), e.Date.Date));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}

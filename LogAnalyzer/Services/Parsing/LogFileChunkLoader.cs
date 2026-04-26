using LogAnalyzer.Models;
using System.Runtime.CompilerServices;
using System.IO;

namespace LogAnalyzer.Services.Parsing;

public sealed class LogFileChunkLoader(ILogParser parser)
{
    private readonly ILogParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public async IAsyncEnumerable<LogLoadChunk> LoadAsync(
        IReadOnlyList<string> fileNames,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (fileNames is null)
        {
            throw new ArgumentNullException(nameof(fileNames));
        }

        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize));
        }

        for (var fileIndex = 0; fileIndex < fileNames.Count; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = fileNames[fileIndex];
            var currentChunk = new List<LogFileEntry>(chunkSize);
            long linesRead = 0;

            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);
            using var reader = new StreamReader(stream);

            LogFileEntry? currentEntry = null;
            List<string>? currentDetail = null;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                linesRead++;

                if (_parser.TryParse(line, out var entry))
                {
                    if (currentEntry is not null)
                    {
                        currentEntry.Detail = currentDetail is { Count: > 0 } ? [.. currentDetail] : [];
                        currentChunk.Add(currentEntry);

                        if (currentChunk.Count >= chunkSize)
                        {
                            yield return new LogLoadChunk([.. currentChunk], fileIndex + 1, fileNames.Count, fileName, linesRead);
                            currentChunk.Clear();
                        }
                    }

                    currentEntry = entry;
                    currentDetail = null;
                }
                else if (currentEntry is not null)
                {
                    currentDetail ??= [];
                    currentDetail.Add(line);
                }
            }

            if (currentEntry is not null)
            {
                currentEntry.Detail = currentDetail is { Count: > 0 } ? [.. currentDetail] : [];
                currentChunk.Add(currentEntry);
            }

            if (currentChunk.Count > 0)
            {
                yield return new LogLoadChunk([.. currentChunk], fileIndex + 1, fileNames.Count, fileName, linesRead);
            }
        }
    }
}

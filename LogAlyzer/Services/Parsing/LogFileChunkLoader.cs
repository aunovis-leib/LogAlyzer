using LogAlyzer.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;

namespace LogAlyzer.Services.Parsing;

public sealed class LogFileChunkLoader(ILogParser parser)
{
    private readonly ILogParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly ILogger<LogFileChunkLoader> _logger = AppServices.CreateLogger<LogFileChunkLoader>();

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
            var stopwatch = Stopwatch.StartNew();
            var currentChunk = new List<LogFileEntry>(chunkSize);
            long linesRead = 0;
            long parsedLines = 0;
            long detailLines = 0;
            long ignoredLines = 0;
            var chunksReturned = 0;

            _logger.LogInformation(
                "Parsing gestartet: Datei {FileName} ({FileIndex} von {FileCount}), Chunkgröße {ChunkSize}",
                Path.GetFileName(fileName),
                fileIndex + 1,
                fileNames.Count,
                chunkSize);

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
                    parsedLines++;
                    if (currentEntry is not null)
                    {
                        currentEntry.Detail = currentDetail is { Count: > 0 } ? [.. currentDetail] : [];
                        currentChunk.Add(currentEntry);

                        if (currentChunk.Count >= chunkSize)
                        {
                            yield return new LogLoadChunk([.. currentChunk], fileIndex + 1, fileNames.Count, fileName, linesRead);
                            chunksReturned++;
                            currentChunk.Clear();
                        }
                    }

                    currentEntry = entry;
                    currentDetail = null;
                }
                else if (currentEntry is not null)
                {
                    detailLines++;
                    currentDetail ??= [];
                    currentDetail.Add(line);
                }
                else
                {
                    ignoredLines++;
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
                chunksReturned++;
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Parsing abgeschlossen: Datei {FileName}, Zeilen {LinesRead}, erkannte Zeilen {ParsedLines}, Detailzeilen {DetailLines}, ignorierte Zeilen {IgnoredLines}, Chunks {ChunksReturned}, Dauer {ElapsedMilliseconds} ms",
                Path.GetFileName(fileName),
                linesRead,
                parsedLines,
                detailLines,
                ignoredLines,
                chunksReturned,
                stopwatch.ElapsedMilliseconds);
        }
    }
}

using LogAlyzer.Models;

namespace LogAlyzer.Services.Parsing;

public sealed record LogLoadChunk(
    IReadOnlyList<LogFileEntry> Entries,
    int FileIndex,
    int FileCount,
    string FileName,
    long LinesRead);

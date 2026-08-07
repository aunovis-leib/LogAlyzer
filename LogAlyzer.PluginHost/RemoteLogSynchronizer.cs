using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

public sealed class RemoteLogSynchronizer
{
    private static readonly JsonSerializerOptions StateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Action<string, Exception?> _log;

    public RemoteLogSynchronizer(Action<string, Exception?>? log = null)
    {
        _log = log ?? ((_, _) => { });
    }

    public async ValueTask<RemoteSyncResult> SynchronizeAsync(
        IRemoteLogSource source,
        string destinationDirectory,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var destinationRoot = Path.GetFullPath(destinationDirectory);
        var archiveDirectory = Path.Combine(
            destinationRoot,
            SanitizePathSegment(source.Descriptor.Id, "remote-source"));
        Directory.CreateDirectory(archiveDirectory);
        Directory.CreateDirectory(pluginDataDirectory);

        var statePath = Path.Combine(
            pluginDataDirectory,
            "remote-sync",
            SanitizeFileName(source.Descriptor.Id) + ".json");
        var state = await LoadStateAsync(statePath, cancellationToken);
        var seenRemoteIds = new HashSet<string>(StringComparer.Ordinal);
        var changedFiles = new List<string>();
        var failures = new List<RemoteSyncFailure>();
        var addedCount = 0;
        var updatedCount = 0;
        var unchangedCount = 0;

        await foreach (var remoteFile in source.ListLogFilesAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(remoteFile.Id)
                || !IsSupportedLogFile(remoteFile.Name))
            {
                continue;
            }

            seenRemoteIds.Add(remoteFile.Id);

            if (state.Files.TryGetValue(remoteFile.Id, out var previous)
                && !NeedsUpdate(previous, remoteFile))
            {
                unchangedCount++;
                continue;
            }

            try
            {
                var relativePath = previous is { LocalRelativePath.Length: > 0 }
                    ? previous.LocalRelativePath
                    : ResolveRelativePath(
                        remoteFile,
                        state.Files.Values,
                        archiveDirectory);
                var localPath = Path.Combine(archiveDirectory, relativePath);
                var temporaryPath = localPath + ".part-" + Guid.NewGuid().ToString("N");

                try
                {
                    var parentDirectory = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrWhiteSpace(parentDirectory))
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }

                    await using var remoteContent = await source.OpenReadAsync(
                        remoteFile,
                        cancellationToken);
                    await using (var localContent = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        FileOptions.SequentialScan))
                    {
                        await remoteContent.CopyToAsync(localContent, cancellationToken);
                        await localContent.FlushAsync(cancellationToken);
                    }

                    File.Move(temporaryPath, localPath, overwrite: true);
                    if (remoteFile.LastModifiedUtc is { } modifiedUtc)
                    {
                        File.SetLastWriteTimeUtc(localPath, modifiedUtc.UtcDateTime);
                    }

                    state.Files[remoteFile.Id] = new SyncStateEntry
                    {
                        LocalRelativePath = relativePath,
                        LastModifiedUtc = remoteFile.LastModifiedUtc,
                        Size = remoteFile.Size,
                        ETag = remoteFile.ETag
                    };
                    changedFiles.Add(localPath);

                    if (previous is null)
                    {
                        addedCount++;
                    }
                    else
                    {
                        updatedCount++;
                    }
                }
                finally
                {
                    TryDelete(temporaryPath);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(new RemoteSyncFailure(remoteFile.Id, remoteFile.Name, ex.Message));
                Log($"Remote-Datei konnte nicht synchronisiert werden: {remoteFile.Name}", ex);
            }
        }

        var missingCount = state.Files.Keys.Count(id => !seenRemoteIds.Contains(id));
        await SaveStateAsync(statePath, state, cancellationToken);

        return new RemoteSyncResult(
            addedCount,
            updatedCount,
            unchangedCount,
            missingCount,
            changedFiles,
            failures);
    }

    private static bool IsSupportedLogFile(string fileName)
    {
        return fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsUpdate(SyncStateEntry previous, RemoteLogFile current)
    {
        if (current.LastModifiedUtc is { } currentModified
            && previous.LastModifiedUtc is { } previousModified)
        {
            if (currentModified > previousModified)
            {
                return true;
            }

            if (currentModified < previousModified)
            {
                return false;
            }
        }

        if (current.ETag is not null
            && previous.ETag is not null
            && !string.Equals(current.ETag, previous.ETag, StringComparison.Ordinal))
        {
            return true;
        }

        if (current.Size is not null
            && previous.Size is not null
            && current.Size != previous.Size)
        {
            return true;
        }

        return current.LastModifiedUtc is null
            && current.ETag is null
            && current.Size is null;
    }

    private static string ResolveRelativePath(
        RemoteLogFile remoteFile,
        IEnumerable<SyncStateEntry> existingEntries,
        string archiveDirectory)
    {
        var preferredPath = SanitizeRelativePath(remoteFile.RelativePath ?? remoteFile.Name);
        var collision = existingEntries.Any(entry =>
            string.Equals(entry.LocalRelativePath, preferredPath, StringComparison.OrdinalIgnoreCase));

        if (!collision || !File.Exists(Path.Combine(archiveDirectory, preferredPath)))
        {
            return preferredPath;
        }

        var extension = Path.GetExtension(preferredPath);
        var withoutExtension = preferredPath[..^extension.Length];
        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(remoteFile.Id)))[..8];
        return withoutExtension + "-" + suffix + extension;
    }

    private static string SanitizeRelativePath(string relativePath)
    {
        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => SanitizePathSegment(segment, "file"))
            .ToArray();

        return segments.Length == 0
            ? "remote-file.log"
            : Path.Combine(segments);
    }

    private static string SanitizePathSegment(string value, string fallback)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();

        return sanitized is "." or ".." or "" ? fallback : sanitized;
    }

    private static string SanitizeFileName(string value)
    {
        return SanitizePathSegment(value, "remote-source");
    }

    private static async Task<SyncState> LoadStateAsync(
        string statePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath))
        {
            return new SyncState();
        }

        try
        {
            await using var stream = File.OpenRead(statePath);
            return await JsonSerializer.DeserializeAsync<SyncState>(
                       stream,
                       StateJsonOptions,
                       cancellationToken)
                   ?? new SyncState();
        }
        catch (JsonException)
        {
            return new SyncState();
        }
        catch (FileNotFoundException)
        {
            return new SyncState();
        }
    }

    private static async Task SaveStateAsync(
        string statePath,
        SyncState state,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = statePath + ".part";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, state, StateJsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, statePath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private void Log(string message, Exception exception)
    {
        try
        {
            _log(message, exception);
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed class SyncState
    {
        public Dictionary<string, SyncStateEntry> Files { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class SyncStateEntry
    {
        public string LocalRelativePath { get; set; } = string.Empty;

        public DateTimeOffset? LastModifiedUtc { get; set; }

        public long? Size { get; set; }

        public string? ETag { get; set; }
    }
}
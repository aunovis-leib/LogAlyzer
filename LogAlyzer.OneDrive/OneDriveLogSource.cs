using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using LogAlyzer.PluginContracts;

namespace LogAlyzer.Plugins.OneDrive;

internal sealed class OneDriveLogSource : IRemoteLogSource
{
    private readonly OneDriveOptions _options;
    private readonly Action<PluginLogLevel, string, Exception?> _log;
    private OneDriveAuthenticator? _authenticator;
    private readonly HttpClient _httpClient;
    private readonly Func<CancellationToken, Task<string>> _accessTokenProvider;
    private readonly SemaphoreSlim _sharedFolderLock = new(1, 1);
    private SharedFolderReference? _sharedFolder;

    public OneDriveLogSource(
        OneDriveOptions options,
        Action<PluginLogLevel, string, Exception?> log,
        HttpClient? httpClient = null,
        Func<CancellationToken, Task<string>>? accessTokenProvider = null)
    {
        _options = options;
        _log = log;
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
        _accessTokenProvider = accessTokenProvider ?? GetAccessTokenAsync;
    }

    public RemoteSourceDescriptor Descriptor { get; } = new(
        "logalyzer.onedrive",
        "OneDrive");

    public async IAsyncEnumerable<RemoteLogFile> ListLogFilesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var accessToken = await _accessTokenProvider(cancellationToken);
        var sharedFolder = await GetSharedFolderAsync(accessToken, cancellationToken);

        await foreach (var remoteFile in EnumerateFolderAsync(
                           accessToken,
                           sharedFolder.DriveId,
                           sharedFolder.ItemId,
                           relativeFolder: string.Empty,
                           cancellationToken))
        {
            yield return remoteFile;
        }
    }

    public async ValueTask<Stream> OpenReadAsync(
        RemoteLogFile file,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var accessToken = await _accessTokenProvider(cancellationToken);
        var sharedFolder = await GetSharedFolderAsync(accessToken, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"drives/{Uri.EscapeDataString(sharedFolder.DriveId)}"
            + $"/items/{Uri.EscapeDataString(file.Id)}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return new HttpResponseStream(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            response);
    }

    private async Task<SharedFolderReference> GetSharedFolderAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (_sharedFolder is not null)
        {
            return _sharedFolder;
        }

        await _sharedFolderLock.WaitAsync(cancellationToken);
        try
        {
            if (_sharedFolder is not null)
            {
                return _sharedFolder;
            }

            var encodedSharingUrl = EncodeSharingUrl(_options.SharedFolderUrl);
            var item = await GetDriveItemAsync(
                accessToken,
                $"shares/{encodedSharingUrl}/driveItem"
                    + "?$select=id,name,parentReference,remoteItem,file,folder",
                cancellationToken);
            var sharedItem = item.RemoteItem ?? item;
            var driveId = item.RemoteItem?.ParentReference?.DriveId
                ?? item.ParentReference?.DriveId;
            var itemId = sharedItem.Id;
            var isFolder = item.Folder is not null || sharedItem.Folder is not null;

            if (!isFolder
                || string.IsNullOrWhiteSpace(driveId)
                || string.IsNullOrWhiteSpace(itemId))
            {
                throw new InvalidDataException(
                    "Der freigegebene OneDrive-Link verweist nicht auf einen auflösbaren Ordner.");
            }

            _sharedFolder = new SharedFolderReference(driveId, itemId);
            return _sharedFolder;
        }
        finally
        {
            _sharedFolderLock.Release();
        }
    }

    private async IAsyncEnumerable<RemoteLogFile> EnumerateFolderAsync(
        string accessToken,
        string driveId,
        string itemId,
        string relativeFolder,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in EnumerateChildrenAsync(
                           accessToken,
                           driveId,
                           itemId,
                           cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.Folder is not null)
            {
                await foreach (var child in EnumerateFolderAsync(
                                   accessToken,
                                   driveId,
                                   item.Id,
                                   CombineRemotePath(relativeFolder, item.Name),
                                   cancellationToken))
                {
                    yield return child;
                }

                continue;
            }

            if (item.File is null || !IsSupportedLogFile(item.Name))
            {
                continue;
            }

            yield return new RemoteLogFile(
                item.Id,
                item.Name,
                CombineRemotePath(relativeFolder, item.Name),
                item.Size,
                item.LastModifiedDateTime,
                item.ETag);
        }
    }

    private async IAsyncEnumerable<DriveItem> EnumerateChildrenAsync(
        string accessToken,
        string driveId,
        string itemId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var nextLink = $"drives/{Uri.EscapeDataString(driveId)}"
            + $"/items/{Uri.EscapeDataString(itemId)}"
            + "/children?$select=id,name,size,lastModifiedDateTime,eTag,file,folder";

        while (!string.IsNullOrWhiteSpace(nextLink))
        {
            var page = await GetPageAsync(accessToken, nextLink, cancellationToken);
            foreach (var item in page.Value)
            {
                yield return item;
            }

            nextLink = page.NextLink;
        }
    }

    private async Task<DriveItem> GetDriveItemAsync(
        string accessToken,
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DriveItem>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Microsoft Graph hat kein Drive-Element zurückgegeben.");
    }

    private async Task<DriveItemPage> GetPageAsync(
        string accessToken,
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DriveItemPage>(cancellationToken: cancellationToken)
            ?? new DriveItemPage();
    }

    private void EnsureConfigured()
    {
        if (!_options.IsClientConfigured)
        {
            throw new InvalidOperationException(
                $"OneDrive ist nicht konfiguriert. ClientId fehlt in {_options.SettingsPath}");
        }

        if (!_options.IsSharedFolderConfigured)
        {
            throw new InvalidOperationException(
                $"OneDrive ist nicht konfiguriert. sharedFolderUrl fehlt in {_options.SettingsPath}");
        }
    }

    private OneDriveAuthenticator GetAuthenticator()
    {
        return _authenticator ??= new OneDriveAuthenticator(_options);
    }

    private Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        return GetAuthenticator().GetAccessTokenAsync(cancellationToken);
    }

    internal static string EncodeSharingUrl(string sharingUrl)
    {
        var normalizedUrl = sharingUrl.Trim();
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "sharedFolderUrl muss eine absolute HTTP- oder HTTPS-URL sein.",
                nameof(sharingUrl));
        }

        return "u!" + Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedUrl))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool IsSupportedLogFile(string name)
    {
        return name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineRemotePath(string folder, string name)
    {
        return string.IsNullOrWhiteSpace(folder) ? name : $"{folder}/{name}";
    }

    private sealed class DriveItemPage
    {
        [JsonPropertyName("value")]
        public List<DriveItem> Value { get; set; } = [];

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    private sealed class DriveItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("lastModifiedDateTime")]
        public DateTimeOffset? LastModifiedDateTime { get; set; }

        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        [JsonPropertyName("file")]
        public object? File { get; set; }

        [JsonPropertyName("folder")]
        public object? Folder { get; set; }

        [JsonPropertyName("parentReference")]
        public ItemReference? ParentReference { get; set; }

        [JsonPropertyName("remoteItem")]
        public DriveItem? RemoteItem { get; set; }
    }

    private sealed class ItemReference
    {
        [JsonPropertyName("driveId")]
        public string? DriveId { get; set; }
    }

    private sealed record SharedFolderReference(
        string DriveId,
        string ItemId);

    private sealed class HttpResponseStream(Stream inner, HttpResponseMessage response) : Stream
    {
        private readonly Stream _inner = inner;
        private readonly HttpResponseMessage _response = response;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _response.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
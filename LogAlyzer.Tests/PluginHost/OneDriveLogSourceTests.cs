using System.Net;
using System.Text;
using LogAlyzer.PluginContracts;
using LogAlyzer.Plugins.OneDrive;
using Xunit;

namespace LogAlyzer.Tests.PluginHost;

public sealed class OneDriveLogSourceTests
{
    [Fact]
    public void EncodeSharingUrl_UsesGraphUrlSafeBase64()
    {
        const string sharingUrl = "https://contoso.example/share/folder?e=abc";
        var expected = "u!" + Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Assert.Equal(expected, OneDriveLogSource.EncodeSharingUrl(sharingUrl));
    }

    [Fact]
    public async Task ListAndOpenReadAsync_UsesResolvedForeignDriveAndItemIds()
    {
        const string sharingUrl = "https://contoso.example/share/folder?e=abc";
        var handler = new GraphHandler(sharingUrl);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
        var options = new OneDriveOptions
        {
            ClientId = "client-id",
            SharedFolderUrl = sharingUrl
        };
        var source = new OneDriveLogSource(
            options,
            (_, _, _) => { },
            httpClient,
            _ => Task.FromResult("access-token"));

        var files = new List<RemoteLogFile>();
        await foreach (var file in source.ListLogFilesAsync(TestContext.Current.CancellationToken))
        {
            files.Add(file);
        }

        var remoteFile = Assert.Single(files);
        Assert.Equal("remote.log", remoteFile.Name);
        Assert.Equal("remote.log", remoteFile.RelativePath);

        await using var contentStream = await source.OpenReadAsync(
            remoteFile,
            TestContext.Current.CancellationToken);
        using var reader = new StreamReader(contentStream);
        Assert.Equal(
            "remote-content",
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                "/v1.0/shares/" + OneDriveLogSource.EncodeSharingUrl(sharingUrl) + "/driveItem",
                "/v1.0/drives/foreign-drive-id/items/foreign-folder-id/children",
                "/v1.0/drives/foreign-drive-id/items/foreign-file-id/content"
            ],
            handler.RequestPaths);
    }

    private sealed class GraphHandler(string sharingUrl) : HttpMessageHandler
    {
        private readonly string _sharedItemPath =
            "/v1.0/shares/" + OneDriveLogSource.EncodeSharingUrl(sharingUrl) + "/driveItem";

        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-token", request.Headers.Authorization?.Parameter);

            var path = request.RequestUri?.AbsolutePath
                ?? throw new InvalidOperationException("Request URI fehlt.");
            RequestPaths.Add(path);

            if (path == _sharedItemPath)
            {
                return Task.FromResult(JsonResponse(
                    """
                    {
                      "id": "shared-wrapper-id",
                      "name": "Shared Logs",
                      "remoteItem": {
                        "id": "foreign-folder-id",
                        "name": "Shared Logs",
                        "parentReference": { "driveId": "foreign-drive-id" },
                        "folder": { "childCount": 1 }
                      }
                    }
                    """));
            }

            if (path == "/v1.0/drives/foreign-drive-id/items/foreign-folder-id/children")
            {
                return Task.FromResult(JsonResponse(
                    """
                    {
                      "value": [
                        {
                          "id": "foreign-file-id",
                          "name": "remote.log",
                          "size": 14,
                          "lastModifiedDateTime": "2026-08-07T08:00:00Z",
                          "eTag": "\"v1\"",
                          "file": {}
                        }
                      ]
                    }
                    """));
            }

            if (path == "/v1.0/drives/foreign-drive-id/items/foreign-file-id/content")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("remote-content", Encoding.UTF8)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
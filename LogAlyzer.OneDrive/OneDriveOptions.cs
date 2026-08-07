using System.Text.Json;

namespace LogAlyzer.Plugins.OneDrive;

internal sealed class OneDriveOptions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string ClientId { get; set; } = string.Empty;

    public string TenantId { get; set; } = "common";

    public string[] Scopes { get; set; } = ["Files.Read"];

    public string SettingsPath { get; private set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    public static async Task<OneDriveOptions> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(dataDirectory);
        var settingsPath = Path.Combine(dataDirectory, "settings.json");

        if (!File.Exists(settingsPath))
        {
            return new OneDriveOptions { SettingsPath = settingsPath };
        }

        try
        {
            await using var stream = File.OpenRead(settingsPath);
            var options = await JsonSerializer.DeserializeAsync<OneDriveOptions>(
                stream,
                JsonOptions,
                cancellationToken) ?? new OneDriveOptions();
            options.SettingsPath = settingsPath;
            options.Scopes = options.Scopes is { Length: > 0 }
                ? options.Scopes
                : ["Files.Read"];
            options.TenantId = string.IsNullOrWhiteSpace(options.TenantId)
                ? "common"
                : options.TenantId;
            return options;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"OneDrive-Einstellungen sind ungültig: {settingsPath}", ex);
        }
    }
}
using Microsoft.Identity.Client;

namespace LogAlyzer.Plugins.OneDrive;

internal sealed class OneDriveAuthenticator
{
    private readonly OneDriveOptions _options;
    private readonly IPublicClientApplication _application;

    public OneDriveAuthenticator(OneDriveOptions options)
    {
        _options = options;
        _application = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, options.TenantId)
            .WithDefaultRedirectUri()
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var accounts = await _application.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account is not null)
        {
            try
            {
                var silentResult = await _application
                    .AcquireTokenSilent(_options.Scopes, account)
                    .ExecuteAsync(cancellationToken);
                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
            }
        }

        var interactiveResult = await _application
            .AcquireTokenInteractive(_options.Scopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);
        return interactiveResult.AccessToken;
    }
}
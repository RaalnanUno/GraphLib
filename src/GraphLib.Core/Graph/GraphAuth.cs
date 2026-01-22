using Microsoft.Identity.Client;

namespace GraphLib.Core.Graph;

/// <summary>
/// Handles Azure AD authentication via Microsoft Identity Client library.
/// Uses app-only authentication (no user interaction), suitable for background services.
/// Obtains access tokens for Microsoft Graph API v1.0 calls.
/// </summary>
public sealed class GraphAuth
{
    private readonly IConfidentialClientApplication _app;

    /// <summary>
    /// Initializes auth with Azure AD credentials.
    /// </summary>
    /// <param name="tenantId">Azure AD tenant GUID</param>
    /// <param name="clientId">Azure AD app registration ID (GUID)</param>
    /// <param name="clientSecret">App registration client secret</param>
    public GraphAuth(string tenantId, string clientId, string clientSecret)
    {
        _app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .Build();
    }

    /// <summary>
    /// Acquires an access token for Microsoft Graph API calls.
    /// The token is valid for ~1 hour and is cached by MSAL.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var result = await _app
            .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
            .ExecuteAsync(ct);

        return result.AccessToken;
    }
}

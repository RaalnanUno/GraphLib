using Microsoft.Identity.Client;

namespace GraphLib.Core.Graph;

public sealed class GraphAuth
{
    private readonly IConfidentialClientApplication _app;

    public GraphAuth(string tenantId, string clientId, string clientSecret)
    {
        _app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var result = await _app
            .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
            .ExecuteAsync(ct);

        return result.AccessToken;
    }
}

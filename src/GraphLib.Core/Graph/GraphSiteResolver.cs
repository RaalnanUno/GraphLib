using System.Net;
using System.Text.Json;

namespace GraphLib.Core.Graph;

/// <summary>
/// Resolves SharePoint site URL to its Microsoft Graph Site ID.
/// Uses the pattern: GET /sites/{hostname}:{server-relative-path}
/// Example: https://tenant.sharepoint.com/sites/SiteName → siteId="tenant.sharepoint.com:/sites/SiteName:"
/// </summary>
public sealed class GraphSiteResolver
{
    private readonly GraphClient _graph;

    public GraphSiteResolver(GraphClient graph) => _graph = graph;

    /// <summary>
    /// Looks up a SharePoint site by URL and returns its Graph ID.
    /// </summary>
    /// <param name="siteUrl">Full SharePoint site URL (must include path, e.g., /sites/SiteName)</param>
    /// <param name="clientRequestId">Request ID for tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple of (siteId, rawJsonResponse) for later use in subsequent Graph calls</returns>
    public async Task<(string siteId, string rawJson)> ResolveSiteAsync(string siteUrl, string clientRequestId, CancellationToken ct)
    {
        var uri = new Uri(siteUrl);

        // Graph format: /sites/{hostname}:{server-relative-path}
        var host = uri.Host;
        var path = uri.AbsolutePath; // e.g. /sites/SiteName
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            throw new ArgumentException("siteUrl must include a site path like https://tenant.sharepoint.com/sites/SiteName");

        var requestPath = $"sites/{host}:{path}";

        using var req = new HttpRequestMessage(HttpMethod.Get, requestPath);
        var resp = await _graph.SendAsync(req, clientRequestId, ct);

        var body = await GraphClient.ReadStringSafeAsync(resp, ct);

        if (resp.StatusCode != HttpStatusCode.OK)
            throw new GraphRequestException("resolveSite failed", resp.StatusCode, body, resp);

        using var doc = JsonDocument.Parse(body);
        var siteId = doc.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(siteId))
            throw new InvalidOperationException("Graph did not return site id.");

        return (siteId!, body);
    }
}

/// <summary>
/// Exception thrown when a Microsoft Graph API call fails.
/// Captures HTTP status, response body, and request/response IDs for debugging.
/// </summary>
public sealed class GraphRequestException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }
    public string? RequestId { get; }
    public string? ClientRequestId { get; }

    public GraphRequestException(string message, System.Net.HttpStatusCode statusCode, string responseBody, HttpResponseMessage resp)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;

        RequestId = TryGetHeader(resp, "request-id");
        ClientRequestId = TryGetHeader(resp, "client-request-id") ?? TryGetHeader(resp, "x-ms-client-request-id");
    }

    private static string? TryGetHeader(HttpResponseMessage resp, string name)
    {
        if (resp.Headers.TryGetValues(name, out var vals))
            return vals.FirstOrDefault();
        return null;
    }
}

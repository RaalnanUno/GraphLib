using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GraphLib.Core.Graph;

/// <summary>
/// HTTP client wrapper for Microsoft Graph API v1.0 endpoint.
/// Automatically injects Bearer token and request tracking headers.
/// All other Graph*Service classes use this client.
/// </summary>
public sealed class GraphClient
{
    private readonly HttpClient _http;
    private readonly GraphAuth _auth;

    /// <summary>
    /// Initializes the Graph client with authentication.
    /// Sets base address to https://graph.microsoft.com/v1.0/
    /// </summary>
    public GraphClient(HttpClient http, GraphAuth auth)
    {
        _http = http;
        _auth = auth;
        _http.BaseAddress ??= new Uri("https://graph.microsoft.com/v1.0/");
    }

    /// <summary>
    /// Sends an HTTP request to Graph API with authorization token and client request ID.
    /// </summary>
    /// <param name="req">The HTTP request to send</param>
    /// <param name="clientRequestId">Optional request ID for tracking (used in request headers)</param>
    /// <param name="ct">Cancellation token for async operation</param>
    /// <returns>HTTP response from Graph API</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string? clientRequestId, CancellationToken ct)
    {
        // Get fresh token and add to Authorization header
        var token = await _auth.GetAccessTokenAsync(ct);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Add request tracking headers for debugging (optional)
        if (!string.IsNullOrWhiteSpace(clientRequestId))
        {
            req.Headers.TryAddWithoutValidation("client-request-id", clientRequestId);
            req.Headers.TryAddWithoutValidation("return-client-request-id", "true");
        }

        return await _http.SendAsync(req, ct);
    }

    /// <summary>
    /// Safely reads response body as string, returns empty string on error.
    /// Used by Graph services to avoid exception bubbling on read failures.
    /// </summary>
    public static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await resp.Content.ReadAsStringAsync(ct); }
        catch { return ""; }
    }

    /// <summary>
    /// Creates a JSON-encoded HTTP content for request body.
    /// Serializes any object to JSON with UTF-8 encoding.
    /// </summary>
    public static StringContent JsonBody(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");
}

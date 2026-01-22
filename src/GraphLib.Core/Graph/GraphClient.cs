using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GraphLib.Core.Graph;

public sealed class GraphClient
{
    private readonly HttpClient _http;
    private readonly GraphAuth _auth;

    public GraphClient(HttpClient http, GraphAuth auth)
    {
        _http = http;
        _auth = auth;
        _http.BaseAddress ??= new Uri("https://graph.microsoft.com/v1.0/");
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string? clientRequestId, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrWhiteSpace(clientRequestId))
        {
            req.Headers.TryAddWithoutValidation("client-request-id", clientRequestId);
            req.Headers.TryAddWithoutValidation("return-client-request-id", "true");
        }

        return await _http.SendAsync(req, ct);
    }

    public static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await resp.Content.ReadAsStringAsync(ct); }
        catch { return ""; }
    }

    public static StringContent JsonBody(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");
}

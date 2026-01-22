using System.Net;
using System.Text.Json;

namespace GraphLib.Core.Graph;

public sealed class GraphDriveResolver
{
    private readonly GraphClient _graph;

    public GraphDriveResolver(GraphClient graph) => _graph = graph;

    public async Task<(string driveId, string rawJson)> ResolveDriveAsync(string siteId, string libraryName, string clientRequestId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"sites/{siteId}/drives");
        var resp = await _graph.SendAsync(req, clientRequestId, ct);
        var body = await GraphClient.ReadStringSafeAsync(resp, ct);

        if (resp.StatusCode != HttpStatusCode.OK)
            throw new GraphRequestException("resolveDrive failed", resp.StatusCode, body, resp);

        using var doc = JsonDocument.Parse(body);
        foreach (var d in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var name = d.GetProperty("name").GetString();
            if (string.Equals(name, libraryName, StringComparison.OrdinalIgnoreCase))
            {
                var id = d.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(id))
                    return (id!, body);
            }
        }

        throw new InvalidOperationException($"Drive (document library) not found by name: '{libraryName}'.");
    }
}

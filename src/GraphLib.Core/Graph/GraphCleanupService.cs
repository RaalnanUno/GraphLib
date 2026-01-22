using System.Net;

namespace GraphLib.Core.Graph;

public sealed class GraphCleanupService
{
    private readonly GraphClient _graph;

    public GraphCleanupService(GraphClient graph) => _graph = graph;

    public async Task DeleteItemAsync(string driveId, string itemId, string clientRequestId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"drives/{driveId}/items/{itemId}");
        var resp = await _graph.SendAsync(req, clientRequestId, ct);

        if (resp.StatusCode != HttpStatusCode.NoContent && resp.StatusCode != HttpStatusCode.OK)
        {
            var body = await GraphClient.ReadStringSafeAsync(resp, ct);
            throw new GraphRequestException("cleanup(delete) failed", resp.StatusCode, body, resp);
        }
    }
}

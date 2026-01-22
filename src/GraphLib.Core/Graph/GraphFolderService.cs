using System.Net;

namespace GraphLib.Core.Graph;

public sealed class GraphFolderService
{
    private readonly GraphClient _graph;

    public GraphFolderService(GraphClient graph) => _graph = graph;

    public async Task EnsureFolderAsync(string driveId, string folderName, string clientRequestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return;

        // Try GET /drives/{driveId}/root:/{folderName}
        using (var get = new HttpRequestMessage(HttpMethod.Get, $"drives/{driveId}/root:/{Uri.EscapeDataString(folderName)}"))
        {
            var resp = await _graph.SendAsync(get, clientRequestId, ct);
            if (resp.StatusCode == HttpStatusCode.OK) return;

            if (resp.StatusCode != HttpStatusCode.NotFound)
            {
                var body = await GraphClient.ReadStringSafeAsync(resp, ct);
                throw new GraphRequestException("ensureFolder(get) failed", resp.StatusCode, body, resp);
            }
        }

        // Create folder: POST /drives/{driveId}/root/children
        // Need dictionary so we can use "@microsoft.graph.conflictBehavior" as a JSON property.
        var createBody = new Dictionary<string, object?>
        {
            ["name"] = folderName,
            ["folder"] = new Dictionary<string, object?>(),
            ["@microsoft.graph.conflictBehavior"] = "fail"
        };

        using var post = new HttpRequestMessage(HttpMethod.Post, $"drives/{driveId}/root/children")
        {
            Content = GraphClient.JsonBody(createBody)
        };

        var postResp = await _graph.SendAsync(post, clientRequestId, ct);
        var postBody = await GraphClient.ReadStringSafeAsync(postResp, ct);

        if (postResp.StatusCode != HttpStatusCode.Created && postResp.StatusCode != HttpStatusCode.OK)
            throw new GraphRequestException("ensureFolder(post) failed", postResp.StatusCode, postBody, postResp);
    }
}

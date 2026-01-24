using System.Net;

namespace GraphLib.Core.Graph;

/// <summary>
/// Creates SharePoint folders if they don't already exist.
/// Uses a two-step pattern: GET (check), then POST (create if missing).
/// </summary>
public sealed class GraphFolderService
{
    private readonly GraphClient _graph;

    public GraphFolderService(GraphClient graph) => _graph = graph;

    /// <summary>
    /// Ensures a folder exists in the SharePoint drive, creating it if necessary.
    /// If folder name is empty/null, does nothing (root folder always exists).
    /// </summary>
    /// <param name="driveId">Graph drive ID</param>
    /// <param name="folderName">Target folder name to ensure exists</param>
    /// <param name="clientRequestId">Request ID for tracking</param>
    /// <param name="ct">Cancellation token</param>
    public async Task EnsureFolderAsync(string driveId, string folderName, string clientRequestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return; // Root always exists

        // Step 1: Try GET to check if folder already exists
        using (var get = new HttpRequestMessage(HttpMethod.Get, $"drives/{driveId}/root:/{Uri.EscapeDataString(folderName)}"))
        {
            var resp = await _graph.SendAsync(get, clientRequestId, ct);
            if (resp.StatusCode == HttpStatusCode.OK) return; // Folder exists, done

            // If not NotFound, there's an actual error
            if (resp.StatusCode != HttpStatusCode.NotFound)
            {
                var body = await GraphClient.ReadStringSafeAsync(resp, ct);
                throw new GraphRequestException("ensureFolder(get) failed", resp.StatusCode, body, resp);
            }
        }

        // Step 2: Create the folder if it doesn't exist
        // POST /drives/{driveId}/root/children with folder definition
        // Note: Dictionary needed because "@microsoft.graph.conflictBehavior" is a special JSON property name
        var createBody = new Dictionary<string, object?>
        {
            ["name"] = folderName,
            ["folder"] = new Dictionary<string, object?>(),
            ["@microsoft.graph.conflictBehavior"] = "fail" // Fail if somehow folder exists (idempotent)
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

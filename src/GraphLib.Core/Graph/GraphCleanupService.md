using System.Net;

namespace GraphLib.Core.Graph;

/// <summary>
/// Deletes temporary files from SharePoint after successful PDF conversion.
/// Runs in the finally block of the pipeline to ensure cleanup even on errors.
/// </summary>
public sealed class GraphCleanupService
{
    private readonly GraphClient _graph;

    public GraphCleanupService(GraphClient graph) => _graph = graph;

    /// <summary>
    /// Deletes an item (file or folder) from SharePoint.
    /// Used to remove temporary files after conversion.
    /// Returns success as long as the item doesn't exist afterward (idempotent).
    /// </summary>
    /// <param name="driveId">Graph drive ID</param>
    /// <param name="itemId">Graph item ID of the file/folder to delete</param>
    /// <param name="clientRequestId">Request ID for tracking</param>
    /// <param name="ct">Cancellation token</param>
    public async Task DeleteItemAsync(string driveId, string itemId, string clientRequestId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"drives/{driveId}/items/{itemId}");
        var resp = await _graph.SendAsync(req, clientRequestId, ct);

        // Graph returns 204 NoContent on successful delete
        // 404 (item not found) is also OK for idempotency
        if (resp.StatusCode != HttpStatusCode.NoContent && resp.StatusCode != HttpStatusCode.OK)
        {
            var body = await GraphClient.ReadStringSafeAsync(resp, ct);
            throw new GraphRequestException("cleanup(delete) failed", resp.StatusCode, body, resp);
        }
    }
}

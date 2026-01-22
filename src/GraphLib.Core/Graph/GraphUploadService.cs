using System.Net;
using System.Text.Json;
using GraphLib.Core.Models;

namespace GraphLib.Core.Graph;

public sealed class GraphUploadService
{
    private readonly GraphClient _graph;

    public GraphUploadService(GraphClient graph) => _graph = graph;

    public async Task<(string itemId, string rawJson)> UploadToFolderAsync(
        string driveId,
        string folderName,
        string fileName,
        byte[] bytes,
        ConflictBehavior conflictBehavior,
        string clientRequestId,
        CancellationToken ct)
    {
        var folderPart = string.IsNullOrWhiteSpace(folderName) ? "" : $"{folderName.TrimEnd('/')}/";
        var path = $"drives/{driveId}/root:/{Uri.EscapeDataString(folderPart + fileName)}:/content";

        // PUT supports: ?@microsoft.graph.conflictBehavior=...
        path += $"?@microsoft.graph.conflictBehavior={conflictBehavior.ToGraphValue()}";

        using var req = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new ByteArrayContent(bytes)
        };

        var resp = await _graph.SendAsync(req, clientRequestId, ct);
        var body = await GraphClient.ReadStringSafeAsync(resp, ct);

        if (resp.StatusCode != HttpStatusCode.Created && resp.StatusCode != HttpStatusCode.OK)
            throw new GraphRequestException("upload failed", resp.StatusCode, body, resp);

        using var doc = JsonDocument.Parse(body);
        var itemId = doc.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(itemId))
            throw new InvalidOperationException("Upload succeeded but item id missing.");

        return (itemId!, body);
    }
}

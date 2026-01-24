using System.Net;
using System.Text.Json;
using GraphLib.Core.Models;

namespace GraphLib.Core.Graph;

/// <summary>
/// Uploads files to SharePoint via Microsoft Graph.
/// Uses PUT request with conflict resolution.
/// Used twice in pipeline: once for temp folder, once for PDF folder.
/// </summary>
public sealed class GraphUploadService
{
    private readonly GraphClient _graph;

    public GraphUploadService(GraphClient graph) => _graph = graph;

    /// <summary>
    /// Uploads a file to a specific folder in a SharePoint drive.
    /// </summary>
    /// <param name="driveId">Graph drive ID</param>
    /// <param name="folderName">Target folder name (empty string = root)</param>
    /// <param name="fileName">Name for the uploaded file</param>
    /// <param name="bytes">File content as byte array</param>
    /// <param name="conflictBehavior">How to handle if file already exists</param>
    /// <param name="clientRequestId">Request ID for tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple of (itemId, rawJsonResponse) where itemId is Graph's unique ID for the uploaded file</returns>
    public async Task<(string itemId, string rawJson)> UploadToFolderAsync(
        string driveId,
        string folderName,
        string fileName,
        byte[] bytes,
        ConflictBehavior conflictBehavior,
        string clientRequestId,
        CancellationToken ct)
    {
        // Build path: /drives/{driveId}/root:/{folderName}/{fileName}:/content
        var folderPart = string.IsNullOrWhiteSpace(folderName) ? "" : $"{folderName.TrimEnd('/')}/";
        var path = $"drives/{driveId}/root:/{Uri.EscapeDataString(folderPart + fileName)}:/content";

        // Apply conflict resolution: ?@microsoft.graph.conflictBehavior=replace|fail|rename
        path += $"?@microsoft.graph.conflictBehavior={conflictBehavior.ToGraphValue()}";

        // PUT the file content to Graph API
        using var req = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new ByteArrayContent(bytes)
        };

        var resp = await _graph.SendAsync(req, clientRequestId, ct);
        var body = await GraphClient.ReadStringSafeAsync(resp, ct);

        // Graph returns 201 Created or 200 OK depending on outcome
        if (resp.StatusCode != HttpStatusCode.Created && resp.StatusCode != HttpStatusCode.OK)
            throw new GraphRequestException("upload failed", resp.StatusCode, body, resp);

        // Extract the new item's ID from response JSON
        using var doc = JsonDocument.Parse(body);
        var itemId = doc.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(itemId))
            throw new InvalidOperationException("Upload succeeded but item id missing.");

        return (itemId!, body);
    }
}

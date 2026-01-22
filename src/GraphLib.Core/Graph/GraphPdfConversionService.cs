using System.Net;

namespace GraphLib.Core.Graph;

/// <summary>
/// Downloads files from SharePoint as PDF via Microsoft Graph conversion.
/// The conversion is done server-side by Graph; no external PDF service is used.
/// Uses the pattern: GET /drives/{driveId}/items/{itemId}/content?format=pdf
/// </summary>
public sealed class GraphPdfConversionService
{
    private readonly GraphClient _graph;

    public GraphPdfConversionService(GraphClient graph) => _graph = graph;

    /// <summary>
    /// Downloads a SharePoint file as a PDF-converted version.
    /// Works with Office documents (Word, Excel, PowerPoint) and other formats.
    /// </summary>
    /// <param name="driveId">Graph drive ID</param>
    /// <param name="itemId">Graph item ID (from upload response)</param>
    /// <param name="clientRequestId">Request ID for tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Raw PDF file bytes</returns>
    public async Task<byte[]> DownloadPdfAsync(string driveId, string itemId, string clientRequestId, CancellationToken ct)
    {
        // Use ?format=pdf query parameter to tell Graph to convert on-the-fly
        var path = $"drives/{driveId}/items/{itemId}/content?format=pdf";

        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        var resp = await _graph.SendAsync(req, clientRequestId, ct);

        if (resp.StatusCode != HttpStatusCode.OK)
        {
            var body = await GraphClient.ReadStringSafeAsync(resp, ct);
            throw new GraphRequestException("convert(download pdf) failed", resp.StatusCode, body, resp);
        }

        // Return the PDF file bytes directly (binary content, not JSON)
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
}

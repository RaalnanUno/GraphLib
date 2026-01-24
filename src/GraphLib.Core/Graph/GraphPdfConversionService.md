using System.Net;
using GraphLib.Core.Data.Repositories;

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

    /// <summary>
    /// Same as DownloadPdfAsync, but also tracks aggregate conversion metrics (no per-file tracking).
    /// You supply the source extension (e.g. ".docx") because Graph only sees itemIds.
    /// </summary>
    public async Task<byte[]> DownloadPdfAsync(
        string driveId,
        string itemId,
        string clientRequestId,
        string? sourceExtension,
        ConversionMetricsRepository metrics,
        CancellationToken ct)
    {
        const string targetExt = ".pdf";

        try
        {
            var bytes = await DownloadPdfAsync(driveId, itemId, clientRequestId, ct);
            await metrics.TrackAsync(sourceExtension, targetExt, success: true, ct);
            return bytes;
        }
        catch
        {
            await metrics.TrackAsync(sourceExtension, targetExt, success: false, ct);
            throw;
        }
    }
}

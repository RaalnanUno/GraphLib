using System.Net;

namespace GraphLib.Core.Graph;

public sealed class GraphPdfConversionService
{
    private readonly GraphClient _graph;

    public GraphPdfConversionService(GraphClient graph) => _graph = graph;

    public async Task<byte[]> DownloadPdfAsync(string driveId, string itemId, string clientRequestId, CancellationToken ct)
    {
        var path = $"drives/{driveId}/items/{itemId}/content?format=pdf";

        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        var resp = await _graph.SendAsync(req, clientRequestId, ct);

        if (resp.StatusCode != HttpStatusCode.OK)
        {
            var body = await GraphClient.ReadStringSafeAsync(resp, ct);
            throw new GraphRequestException("convert(download pdf) failed", resp.StatusCode, body, resp);
        }

        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
}

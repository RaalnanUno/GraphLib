using System.Net;
using System.Text.Json;

namespace GraphLib.Core.Graph;

/// <summary>
/// Resolves a SharePoint document library name to its Microsoft Graph Drive ID.
/// A "drive" in Graph terminology is a document library in SharePoint.
/// Uses the pattern: GET /sites/{siteId}/drives, then searches by name.
/// </summary>
public sealed class GraphDriveResolver
{
    private readonly GraphClient _graph;

    public GraphDriveResolver(GraphClient graph) => _graph = graph;

    /// <summary>
    /// Finds the drive (library) ID by looking up its name within a SharePoint site.
    /// </summary>
    /// <param name="siteId">Graph site ID (from GraphSiteResolver)</param>
    /// <param name="libraryName">Name of the document library to find</param>
    /// <param name="clientRequestId">Request ID for tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple of (driveId, rawJsonResponse)</returns>
    public async Task<(string driveId, string rawJson)> ResolveDriveAsync(string siteId, string libraryName, string clientRequestId, CancellationToken ct)
    {
        // GET all drives in this site
        using var req = new HttpRequestMessage(HttpMethod.Get, $"sites/{siteId}/drives");
        var resp = await _graph.SendAsync(req, clientRequestId, ct);
        var body = await GraphClient.ReadStringSafeAsync(resp, ct);

        if (resp.StatusCode != HttpStatusCode.OK)
            throw new GraphRequestException("resolveDrive failed", resp.StatusCode, body, resp);

        // Parse JSON response and find library by name (case-insensitive)
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

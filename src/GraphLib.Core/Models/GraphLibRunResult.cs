namespace GraphLib.Core.Models;

public sealed class GraphLibRunResult
{
    public required string RunId { get; init; }
    public required bool Success { get; init; }
    public required string Summary { get; init; }
    public required long InputBytes { get; init; }
    public required long PdfBytes { get; init; }
    public required TimeSpan Elapsed { get; init; }
}

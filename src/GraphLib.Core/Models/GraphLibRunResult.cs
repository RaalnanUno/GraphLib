namespace GraphLib.Core.Models;

/// <summary>
/// Result returned by SingleFilePipeline.RunAsync().
/// Contains success status, metrics, and timing information.
/// </summary>
public sealed class GraphLibRunResult
{
    /// <summary>Unique run ID for tracking</summary>
    public required string RunId { get; init; }
    
    /// <summary>Whether the run succeeded (true) or failed (false)</summary>
    public required bool Success { get; init; }
    
    /// <summary>Human-readable summary (e.g., "OK file='document.docx' pdfBytes=50000")</summary>
    public required string Summary { get; init; }
    
    /// <summary>Size in bytes of the input file</summary>
    public required long InputBytes { get; init; }
    
    /// <summary>Size in bytes of the generated PDF</summary>
    public required long PdfBytes { get; init; }
    
    /// <summary>Total elapsed time for the run</summary>
    public required TimeSpan Elapsed { get; init; }
}

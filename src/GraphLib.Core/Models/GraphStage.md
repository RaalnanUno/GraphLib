namespace GraphLib.Core.Models;

/// <summary>
/// Constants for pipeline stages (used in logging and error messages).
/// Each stage represents a major operation in the conversion workflow.
/// </summary>
public static class GraphStage
{
    /// <summary>Resolving SharePoint site URL to Graph site ID</summary>
    public const string ResolveSite = "resolveSite";
    
    /// <summary>Resolving document library name to Graph drive ID</summary>
    public const string ResolveDrive = "resolveDrive";
    
    /// <summary>Creating/ensuring temporary and PDF folders exist</summary>
    public const string EnsureFolder = "ensureFolder";
    
    /// <summary>Uploading file to temporary folder</summary>
    public const string Upload = "upload";
    
    /// <summary>Converting file to PDF via Graph API</summary>
    public const string Convert = "convert";
    
    /// <summary>Storing generated PDF back into SharePoint</summary>
    public const string StorePdf = "storePdf";
    
    /// <summary>Deleting temporary file after conversion</summary>
    public const string Cleanup = "cleanup";
}

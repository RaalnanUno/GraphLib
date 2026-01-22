namespace GraphLib.ConsoleApp.Cli;

/// <summary>
/// Container for all command-line arguments parsed from argv.
/// These properties can override database settings.
/// All properties are nullable: null means the argument was not provided.
/// </summary>
public sealed class Args
{
    /// <summary>
    /// The command to execute: "init" or "run". Defaults to "run".
    /// </summary>
    public string Command { get; set; } = "run";

    /// <summary>
    /// Input file path (required for "run" command).
    /// When ProcessFolderMode=true, this becomes a folder path instead.
    /// </summary>
    public string? File { get; set; }
    
    /// <summary>
    /// Database path override. If null, uses ./Data/GraphLib.db relative to exe.
    /// </summary>
    public string? Db { get; set; }
    
    /// <summary>
    /// SharePoint site URL (e.g., https://tenant.sharepoint.com/sites/SiteName).
    /// </summary>
    public string? SiteUrl { get; set; }
    
    /// <summary>
    /// Name of the document library in SharePoint (e.g., "Shared Documents").
    /// </summary>
    public string? LibraryName { get; set; }
    
    /// <summary>
    /// Name of the temporary folder for uploads before PDF conversion.
    /// </summary>
    public string? TempFolder { get; set; }
    
    /// <summary>
    /// Name of the folder where PDFs are stored (empty string to disable).
    /// </summary>
    public string? PdfFolder { get; set; }
    
    /// <summary>
    /// Whether to delete temporary files after successful conversion.
    /// </summary>
    public bool? CleanupTemp { get; set; }
    
    /// <summary>
    /// How to handle filename conflicts: "fail", "replace", or "rename".
    /// </summary>
    public string? ConflictBehavior { get; set; }
    
    /// <summary>
    /// Custom run ID for tracking. If null, a new GUID is generated.
    /// </summary>
    public string? RunId { get; set; }
    
    /// <summary>
    /// If true, only log failures (not successes) to reduce verbosity.
    /// </summary>
    public bool? LogFailuresOnly { get; set; }

    // Advanced settings (supported but not shown in basic help)
    
    /// <summary>
    /// Whether to save the PDF back into SharePoint.
    /// </summary>
    public bool? StorePdfInSharePoint { get; set; }
    
    /// <summary>
    /// Process all files in a folder instead of a single file.
    /// </summary>
    public bool? ProcessFolderMode { get; set; }
    
    /// <summary>
    /// In folder mode, continue processing even if one file fails.
    /// </summary>
    public bool? IgnoreFailuresWhenFolderMode { get; set; }
    
    // Azure AD authentication (usually from database)
    
    /// <summary>
    /// Azure AD tenant GUID.
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Azure AD application (client) GUID.
    /// </summary>
    public string? ClientId { get; set; }
    
    /// <summary>
    /// Azure AD client secret.
    /// </summary>
    public string? ClientSecret { get; set; }
}

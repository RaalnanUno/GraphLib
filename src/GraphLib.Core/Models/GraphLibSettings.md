namespace GraphLib.Core.Models;

/// <summary>
/// Application settings for GraphLib.
/// Stored in SQLite AppSettings table (row Id=1).
/// All properties are required (sealed record with required keyword).
/// Used immutably throughout the application.
/// </summary>
public sealed record GraphLibSettings
{
    // === TARGET ===
    
    /// <summary>Full SharePoint site URL (e.g., https://tenant.sharepoint.com/sites/SiteName)</summary>
    public required string SiteUrl { get; init; }
    
    /// <summary>Document library name (e.g., "Shared Documents")</summary>
    public required string LibraryName { get; init; }
    
    /// <summary>Folder name for temporary files before conversion</summary>
    public required string TempFolder { get; init; }
    
    /// <summary>Folder name for storing converted PDFs (empty string disables)</summary>
    public required string PdfFolder { get; init; }

    // === BEHAVIOR ===
    
    /// <summary>Whether to delete temporary files after successful conversion</summary>
    public required bool CleanupTemp { get; init; }
    
    /// <summary>How to handle filename conflicts: Fail, Replace, or Rename</summary>
    public required ConflictBehavior ConflictBehavior { get; init; }

    // === TOGGLES ===
    
    /// <summary>Whether to store the PDF back into SharePoint</summary>
    public required bool StorePdfInSharePoint { get; init; }
    
    /// <summary>Process all files in a folder instead of single file</summary>
    public required bool ProcessFolderMode { get; init; }
    
    /// <summary>In folder mode, continue processing even if individual file fails</summary>
    public required bool IgnoreFailuresWhenFolderMode { get; init; }

    // === AUTHENTICATION ===
    
    /// <summary>Azure AD tenant GUID</summary>
    public required string TenantId { get; init; }
    
    /// <summary>Azure AD app registration GUID</summary>
    public required string ClientId { get; init; }
    
    /// <summary>Azure AD app client secret</summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Factory method providing default values for initial setup.
    /// Used by DbInitializer when seeding a new database.
    /// All values are placeholders and should be updated via SQLite client.
    /// </summary>
    public static GraphLibSettings DefaultForInit() => new()
    {
        SiteUrl = "https://tenant.sharepoint.com/sites/SiteName",
        LibraryName = "Shared Documents",
        TempFolder = "GraphLibTemp",
        PdfFolder = "GraphLibPdf",
        CleanupTemp = true,
        ConflictBehavior = ConflictBehavior.Replace,
        StorePdfInSharePoint = true,
        ProcessFolderMode = false,
        IgnoreFailuresWhenFolderMode = true,
        TenantId = "00000000-0000-0000-0000-000000000000",
        ClientId = "00000000-0000-0000-0000-000000000000",
        ClientSecret = "REPLACE_ME"
    };
}

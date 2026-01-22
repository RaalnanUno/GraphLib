namespace GraphLib.Core.Models;

public sealed record GraphLibSettings
{
    // Target
    public required string SiteUrl { get; init; }
    public required string LibraryName { get; init; }
    public required string TempFolder { get; init; }
    public required string PdfFolder { get; init; }

    // Behavior
    public required bool CleanupTemp { get; init; }
    public required ConflictBehavior ConflictBehavior { get; init; }

    // Toggles
    public required bool StorePdfInSharePoint { get; init; }
    public required bool ProcessFolderMode { get; init; }
    public required bool IgnoreFailuresWhenFolderMode { get; init; }

    // Auth
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }

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

namespace GraphLib.ConsoleApp.Cli;

public sealed class Args
{
    public string Command { get; set; } = "run";

    public string? File { get; set; }             // required for run
    public string? Db { get; set; }               // db path
    public string? SiteUrl { get; set; }
    public string? LibraryName { get; set; }
    public string? TempFolder { get; set; }
    public string? PdfFolder { get; set; }
    public bool? CleanupTemp { get; set; }
    public string? ConflictBehavior { get; set; }
    public string? RunId { get; set; }
    public bool? LogFailuresOnly { get; set; }

    // overrides not required in v1 help but supported:
    public bool? StorePdfInSharePoint { get; set; }
    public bool? ProcessFolderMode { get; set; }
    public bool? IgnoreFailuresWhenFolderMode { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

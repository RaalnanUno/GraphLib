namespace GraphLib.ConsoleApp.Cli;

/// <summary>
/// Static helper class for printing help text and usage instructions to the console.
/// </summary>
public static class Commands
{
    /// <summary>
    /// Prints the full help text showing all available commands and options.
    /// Called by the "help", "--help", or "-h" command.
    /// </summary>
    public static void PrintHelp()
    {
        System.Console.WriteLine(@"
GraphLib

Commands:
  graphlib init --db ""./Data/GraphLib.db""
  graphlib run  --file ""C:\path\input.docx"" [--db ""./Data/GraphLib.db""] [overrides...]

Run overrides (any setting can be overridden):
  --siteUrl ""https://tenant.sharepoint.com/sites/SiteName""
  --libraryName ""Shared Documents""
  --tempFolder ""GraphLibTemp""
  --pdfFolder ""GraphLibPdf""   (empty string disables store)
  --cleanupTemp true|false
  --conflictBehavior fail|replace|rename
  --runId <guid>
  --logFailuresOnly true|false

Auth overrides:
  --tenantId <guid>
  --clientId <guid>
  --clientSecret <secret>

Notes:
  - All settings (except --file) are stored in SQLite AppSettings (Id=1).
  - `init` creates DB + seeds reference tables and inserts a default AppSettings row.
");
    }
}

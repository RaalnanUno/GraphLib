using GraphLib.ConsoleApp.Cli;
using GraphLib.Core.Data;
using GraphLib.Core.Data.Repositories;
using GraphLib.Core.Graph;
using GraphLib.Core.Models;
using GraphLib.Core.Pipeline;
using GraphLib.Core.Secrets;

// ENTRY POINT: Parse command-line arguments (e.g., "run --file document.docx")
var argsParsed = ArgsParser.Parse(args);

// Handle help command
if (argsParsed.Command is "help" or "--help" or "-h")
{
    Commands.PrintHelp();
    return 0;
}

// Resolve database path: use --db override, or default to ./Data/GraphLib.db (relative to exe)
var dbPath = SqlitePaths.ResolveDbPath(argsParsed.Db, "./Data/GraphLib.db");
var dbFactory = new DbConnectionFactory(dbPath);

// INIT COMMAND: Create database schema and seed default AppSettings
if (argsParsed.Command == "init")
{
    var init = new DbInitializer(dbFactory);
    init.EnsureCreatedAndSeedDefaults();

    System.Console.WriteLine($"OK init db='{dbPath}' (AppSettings seeded with placeholders).");
    System.Console.WriteLine("Next: update AppSettings (TenantId/ClientId/ClientSecret/SiteUrl/etc) via SQLite client.");
    return 0;
}

// Validate that "run" is the command
if (argsParsed.Command != "run")
{
    System.Console.WriteLine($"Unknown command: {argsParsed.Command}");
    Commands.PrintHelp();
    return 2;
}

// RUN COMMAND: Validate required --file argument
if (string.IsNullOrWhiteSpace(argsParsed.File))
{
    System.Console.WriteLine("Missing required --file");
    return 2;
}

// Load settings from SQLite database (AppSettings table, Id=1)
var secretProvider = new DbSecretProvider();
var settingsRepo = new SettingsRepository(dbFactory, secretProvider);
var s = settingsRepo.Get();

// Apply command-line overrides: CLI arguments take precedence over database settings
s = ApplyOverrides(s, argsParsed);

// Build all core Graph service instances (dependency injection pattern)
using var http = new HttpClient();
var auth = new GraphAuth(s.TenantId, s.ClientId, s.ClientSecret);
var graph = new GraphClient(http, auth);

// Graph services for different operations
var siteResolver = new GraphSiteResolver(graph);
var driveResolver = new GraphDriveResolver(graph);
var folderSvc = new GraphFolderService(graph);
var uploadSvc = new GraphUploadService(graph);
var convertSvc = new GraphPdfConversionService(graph);
var cleanupSvc = new GraphCleanupService(graph);

// Data repositories for logging
var runRepo = new RunRepository(dbFactory);
var fileRepo = new FileEventRepository(dbFactory);
var logRepo = new EventLogRepository(dbFactory);

// Assemble the pipeline: orchestrator for the entire workflow
var pipeline = new SingleFilePipeline(
    siteResolver,
    driveResolver,
    folderSvc,
    uploadSvc,
    convertSvc,
    uploadSvc,
    cleanupSvc,
    runRepo,
    fileRepo,
    logRepo
);

// Generate a unique run ID for tracking all files in this execution
var runId = string.IsNullOrWhiteSpace(argsParsed.RunId) ? Guid.NewGuid().ToString() : argsParsed.RunId!;
var logFailuresOnly = argsParsed.LogFailuresOnly ?? false;

try
{
    // Execute the pipeline: upload → convert PDF → optionally store PDF → cleanup
    var result = await pipeline.RunAsync(runId, argsParsed.File!, s, logFailuresOnly, CancellationToken.None);
    System.Console.WriteLine($"{(result.Success ? "OK" : "FAIL")} runId={result.RunId} elapsedMs={(int)result.Elapsed.TotalMilliseconds} inputBytes={result.InputBytes} pdfBytes={result.PdfBytes}");
    return result.Success ? 0 : 1;
}
catch (Exception ex)
{
    System.Console.WriteLine($"FAIL runId={runId} ({ex.GetType().Name})");
    return 1;
}

/// <summary>
/// Merges CLI argument overrides with database settings.
/// CLI arguments take precedence: if --siteUrl is passed, it overrides the DB value.
/// Uses "with" expression on the sealed record to create a new immutable copy with changes.
/// </summary>
static GraphLibSettings ApplyOverrides(GraphLibSettings s, Args a)
{
    return s with
    {
        SiteUrl = a.SiteUrl ?? s.SiteUrl,
        LibraryName = a.LibraryName ?? s.LibraryName,
        TempFolder = a.TempFolder ?? s.TempFolder,
        PdfFolder = a.PdfFolder ?? s.PdfFolder,
        CleanupTemp = a.CleanupTemp ?? s.CleanupTemp,
        ConflictBehavior = ConflictBehaviorExtensions.Parse(a.ConflictBehavior, s.ConflictBehavior),

        StorePdfInSharePoint = a.StorePdfInSharePoint ?? s.StorePdfInSharePoint,
        ProcessFolderMode = a.ProcessFolderMode ?? s.ProcessFolderMode,
        IgnoreFailuresWhenFolderMode = a.IgnoreFailuresWhenFolderMode ?? s.IgnoreFailuresWhenFolderMode,

        TenantId = a.TenantId ?? s.TenantId,
        ClientId = a.ClientId ?? s.ClientId,
        ClientSecret = a.ClientSecret ?? s.ClientSecret,
    };
}

using GraphLib.ConsoleApp.Cli;
using GraphLib.Core.Data;
using GraphLib.Core.Data.Repositories;
using GraphLib.Core.Graph;
using GraphLib.Core.Models;
using GraphLib.Core.Pipeline;
using GraphLib.Core.Secrets;

var argsParsed = ArgsParser.Parse(args);

if (argsParsed.Command is "help" or "--help" or "-h")
{
    Commands.PrintHelp();
    return 0;
}

var dbPath = SqlitePaths.ResolveDbPath(argsParsed.Db, "./Data/GraphLib.db");
var dbFactory = new DbConnectionFactory(dbPath);

if (argsParsed.Command == "init")
{
    var init = new DbInitializer(dbFactory);
    init.EnsureCreatedAndSeedDefaults();

    System.Console.WriteLine($"OK init db='{dbPath}' (AppSettings seeded with placeholders).");
    System.Console.WriteLine("Next: update AppSettings (TenantId/ClientId/ClientSecret/SiteUrl/etc) via SQLite client.");
    return 0;
}

if (argsParsed.Command != "run")
{
    System.Console.WriteLine($"Unknown command: {argsParsed.Command}");
    Commands.PrintHelp();
    return 2;
}

if (string.IsNullOrWhiteSpace(argsParsed.File))
{
    System.Console.WriteLine("Missing required --file");
    return 2;
}

// Load settings from DB
var secretProvider = new DbSecretProvider();
var settingsRepo = new SettingsRepository(dbFactory, secretProvider);
var s = settingsRepo.Get();

// Apply CLI overrides (CLI wins)
s = ApplyOverrides(s, argsParsed);

// Build Core services
using var http = new HttpClient();
var auth = new GraphAuth(s.TenantId, s.ClientId, s.ClientSecret);
var graph = new GraphClient(http, auth);

var siteResolver = new GraphSiteResolver(graph);
var driveResolver = new GraphDriveResolver(graph);
var folderSvc = new GraphFolderService(graph);
var uploadSvc = new GraphUploadService(graph);
var convertSvc = new GraphPdfConversionService(graph);
var cleanupSvc = new GraphCleanupService(graph);

var runRepo = new RunRepository(dbFactory);
var fileRepo = new FileEventRepository(dbFactory);
var logRepo = new EventLogRepository(dbFactory);

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

var runId = string.IsNullOrWhiteSpace(argsParsed.RunId) ? Guid.NewGuid().ToString() : argsParsed.RunId!;
var logFailuresOnly = argsParsed.LogFailuresOnly ?? false;

try
{
    var result = await pipeline.RunAsync(runId, argsParsed.File!, s, logFailuresOnly, CancellationToken.None);
    System.Console.WriteLine($"{(result.Success ? "OK" : "FAIL")} runId={result.RunId} elapsedMs={(int)result.Elapsed.TotalMilliseconds} inputBytes={result.InputBytes} pdfBytes={result.PdfBytes}");
    return result.Success ? 0 : 1;
}
catch (Exception ex)
{
    System.Console.WriteLine($"FAIL runId={runId} ({ex.GetType().Name})");
    return 1;
}

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

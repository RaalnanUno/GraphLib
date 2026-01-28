# I want to extract the core functions of GraphLib into a single class.

It will run int a .Net 4.8 project.
Please be sure to include documentation in the code, and include a joke somewhere in the comments for the devs.
Keep in mind that we will only deal with the PDF conversion at this point.

IF (and that's a big if) you can set up a SQLite db that will log errors silently that would be cool, but it needs to not interfere with the basic operation. It shouldn't store credentials or require any addional steps for setup.
If that can't be done, it's no big deal.
If you can do the SQLite logging, we can put that in a separate class to make it reuseable in other parts of the app. Be sure to include file and method name in the logging.
Please adapt this to match our existing GraphLib naming conventions (GraphStage, GraphLibRunResult, etc.) and include usage examples as comments in the code.
Also, add a lightweight GraphLibRunResult.Logs collection so our calling app can optionally surface what happened—without needing the full DB pipeline and swap the synchronous calls for async.
```xml
<files>
  <file path="src\GraphLib.Console\Cli\Args.cs" />
  <file path="src\GraphLib.Console\Cli\ArgsParser.cs" />
  <file path="src\GraphLib.Console\Cli\Commands.cs" />
  <file path="src\GraphLib.Console\GraphLib.Console.csproj" />
  <file path="src\GraphLib.Console\Program.cs" />
  <file path="src\GraphLib.Core.Tests\Cli\ArgsParserTests.cs" />
  <file path="src\GraphLib.Core.Tests\Data\SqlitePathsTests.cs" />
  <file path="src\GraphLib.Core.Tests\Graph\GraphSmokeTests.cs" />
  <file path="src\GraphLib.Core.Tests\GraphLib.Core.Tests.csproj" />
  <file path="src\GraphLib.Core.Tests\Models\ConflictBehaviorTests.cs" />
  <file path="src\GraphLib.Core.Tests\Pipeline\PipelineEventsTests.cs" />
  <file path="src\GraphLib.Core.Tests\Secrets\DbSecretProviderTests.cs" />
  <file path="src\GraphLib.Core\Data\DbConnectionFactory.cs" />
  <file path="src\GraphLib.Core\Data\DbInitializer.cs" />
  <file path="src\GraphLib.Core\Data\Repositories\EventLogRepository.cs" />
  <file path="src\GraphLib.Core\Data\Repositories\FileEventRepository.cs" />
  <file path="src\GraphLib.Core\Data\Repositories\RunRepository.cs" />
  <file path="src\GraphLib.Core\Data\Repositories\SettingsRepository.cs" />
  <file path="src\GraphLib.Core\Data\SqlitePaths.cs" />
  <file path="src\GraphLib.Core\Graph\GraphAuth.cs" />
  <file path="src\GraphLib.Core\Graph\GraphCleanupService.cs" />
  <file path="src\GraphLib.Core\Graph\GraphClient.cs" />
  <file path="src\GraphLib.Core\Graph\GraphDriveResolver.cs" />
  <file path="src\GraphLib.Core\Graph\GraphFolderService.cs" />
  <file path="src\GraphLib.Core\Graph\GraphPdfConversionService.cs" />
  <file path="src\GraphLib.Core\Graph\GraphSiteResolver.cs" />
  <file path="src\GraphLib.Core\Graph\GraphUploadService.cs" />
  <file path="src\GraphLib.Core\GraphLib.Core.csproj" />
  <file path="src\GraphLib.Core\Models\ConflictBehavior.cs" />
  <file path="src\GraphLib.Core\Models\GraphLibRunResult.cs" />
  <file path="src\GraphLib.Core\Models\GraphLibSettings.cs" />
  <file path="src\GraphLib.Core\Models\GraphStage.cs" />
  <file path="src\GraphLib.Core\Models\LogLevel.cs" />
  <file path="src\GraphLib.Core\Pipeline\PipelineEvents.cs" />
  <file path="src\GraphLib.Core\Pipeline\SingleFilePipeline.cs" />
  <file path="src\GraphLib.Core\Secrets\DbSecretProvider.cs" />
  <file path="src\GraphLib.Core\Secrets\ISecretProvider.cs" />
  <file path="src\GraphLib.Core\Sql\schema.sql" />
  <file path="src\GraphLib.sln" />
</files>

```

## src\GraphLib.Console\Program.cs

```cs
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

```

## src\GraphLib.Core\Pipeline\SingleFilePipeline.cs

```cs
using System.Diagnostics;
using GraphLib.Core.Data.Repositories;
using GraphLib.Core.Graph;
using GraphLib.Core.Models;

namespace GraphLib.Core.Pipeline;

/// <summary>
/// Orchestrates the complete file conversion workflow.
/// This is the "heartbeat" of the application.
/// 
/// Workflow:
/// 1. Resolve SharePoint site URL → site ID
/// 2. Resolve document library name → drive ID
/// 3. Create/ensure temp and PDF folders exist
/// 4. Upload file to temp folder
/// 5. Convert file to PDF via Graph API
/// 6. (Optional) Store PDF back in SharePoint
/// 7. (Optional) Delete temporary file
/// 
/// All operations are logged to EventLogs table (JSON payloads).
/// Runs are tracked in Runs and FileEvents tables.
/// </summary>
public sealed class SingleFilePipeline
{
    // Graph service instances (all dependencies injected in constructor)
    private readonly GraphSiteResolver _siteResolver;
    private readonly GraphDriveResolver _driveResolver;
    private readonly GraphFolderService _folders;
    private readonly GraphUploadService _upload;
    private readonly GraphPdfConversionService _convert;
    private readonly GraphUploadService _storePdf;
    private readonly GraphCleanupService _cleanup;

    // Data repositories for logging
    private readonly RunRepository _runs;
    private readonly FileEventRepository _files;
    private readonly EventLogRepository _logs;

    /// <summary>
    /// Initializes pipeline with all required dependencies.
    /// Uses constructor injection pattern (no runtime service discovery).
    /// </summary>
    public SingleFilePipeline(
        GraphSiteResolver siteResolver,
        GraphDriveResolver driveResolver,
        GraphFolderService folders,
        GraphUploadService upload,
        GraphPdfConversionService convert,
        GraphUploadService storePdf,
        GraphCleanupService cleanup,
        RunRepository runs,
        FileEventRepository files,
        EventLogRepository logs)
    {
        _siteResolver = siteResolver;
        _driveResolver = driveResolver;
        _folders = folders;
        _upload = upload;
        _convert = convert;
        _storePdf = storePdf;
        _cleanup = cleanup;

        _runs = runs;
        _files = files;
        _logs = logs;
    }

    /// <summary>
    /// Executes the complete pipeline for a single file.
    /// Returns immediately with result; all operations are async.
    /// Logs all stages to database even on failure.
    /// </summary>
    /// <param name="runId">Unique ID for tracking this execution</param>
    /// <param name="filePath">Path to input file</param>
    /// <param name="settings">SharePoint and auth configuration</param>
    /// <param name="logFailuresOnly">If true, only log failures (not successes)</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>GraphLibRunResult with success status and metrics</returns>
    public async Task<GraphLibRunResult> RunAsync(
        string runId,
        string filePath,
        GraphLibSettings settings,
        bool logFailuresOnly,
        CancellationToken ct)
    {
        // Start timing this run
        var sw = Stopwatch.StartNew();
        var started = DateTimeOffset.UtcNow;

        // Record run start in database
        _runs.InsertRunStarted(runId, started);

        // Load input file and validate it exists
        var fi = new FileInfo(filePath);
        if (!fi.Exists) throw new FileNotFoundException("Input file not found.", filePath);

        var fileName = fi.Name;
        var ext = fi.Extension.TrimStart('.').ToLowerInvariant();
        var inputBytes = await File.ReadAllBytesAsync(fi.FullName, ct);

        // Record file event start
        var fileEventId = _files.InsertFileStarted(runId, fi.FullName, fileName, ext, inputBytes.LongLength, DateTimeOffset.UtcNow);

        // Track state across stages (populated as workflow progresses)
        string? driveId = null;
        string? tempItemId = null;
        string? pdfItemId = null;
        byte[] pdfBytes = Array.Empty<byte>();

        bool success = false;

        try
        {
            // Generate a unique client request ID for tracing across all Graph calls
            var clientReq = Guid.NewGuid().ToString();

            // STAGE 1: Resolve site
            var (siteId, _) = await _siteResolver.ResolveSiteAsync(settings.SiteUrl, clientReq, ct);
            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.ResolveSite, new
            {
                runId, stage = GraphStage.ResolveSite, success = true, siteId, file = new { path = fi.FullName, name = fileName, extension = ext, sizeBytes = inputBytes.LongLength }
            });

            // STAGE 2: Resolve drive
            (driveId, _) = await _driveResolver.ResolveDriveAsync(siteId, settings.LibraryName, clientReq, ct);
            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.ResolveDrive, new
            {
                runId, stage = GraphStage.ResolveDrive, success = true, siteId, driveId, libraryName = settings.LibraryName
            });

            // STAGE 3: Ensure folders exist
            await _folders.EnsureFolderAsync(driveId, settings.TempFolder, clientReq, ct);
            if (settings.StorePdfInSharePoint && !string.IsNullOrWhiteSpace(settings.PdfFolder))
                await _folders.EnsureFolderAsync(driveId, settings.PdfFolder, clientReq, ct);

            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.EnsureFolder, new
            {
                runId, stage = GraphStage.EnsureFolder, success = true, tempFolder = settings.TempFolder, pdfFolder = settings.PdfFolder
            });

            // STAGE 4: Upload file to temporary folder
            (tempItemId, _) = await _upload.UploadToFolderAsync(
                driveId,
                settings.TempFolder,
                fileName,
                inputBytes,
                settings.ConflictBehavior,
                clientReq,
                ct);

            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.Upload, new
            {
                runId, stage = GraphStage.Upload, success = true, driveId, tempItemId, conflictBehavior = settings.ConflictBehavior.ToGraphValue()
            });

            // STAGE 5: Convert file to PDF (download from Graph with ?format=pdf)
            pdfBytes = await _convert.DownloadPdfAsync(driveId, tempItemId, clientReq, ct);

            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.Convert, new
            {
                runId, stage = GraphStage.Convert, success = true, driveId, tempItemId, pdfBytes = pdfBytes.LongLength
            });

            // STAGE 6: Store PDF (optional - only if enabled in settings)
            if (settings.StorePdfInSharePoint && !string.IsNullOrWhiteSpace(settings.PdfFolder))
            {
                var pdfName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";

                (pdfItemId, _) = await _storePdf.UploadToFolderAsync(
                    driveId,
                    settings.PdfFolder,
                    pdfName,
                    pdfBytes,
                    settings.ConflictBehavior,
                    clientReq,
                    ct);

                MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.StorePdf, new
                {
                    runId, stage = GraphStage.StorePdf, success = true, driveId, pdfItemId, pdfName, pdfFolder = settings.PdfFolder
                });
            }

            // STAGE 7: Cleanup (optional - only if enabled in settings)
            if (settings.CleanupTemp && tempItemId is not null)
            {
                await _cleanup.DeleteItemAsync(driveId, tempItemId, clientReq, ct);

                MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.Cleanup, new
                {
                    runId, stage = GraphStage.Cleanup, success = true, driveId, tempItemId
                });
            }

            // All stages completed successfully
            success = true;
            return new GraphLibRunResult
            {
                RunId = runId,
                Success = true,
                Summary = $"OK file='{fileName}' pdfBytes={pdfBytes.LongLength}",
                InputBytes = inputBytes.LongLength,
                PdfBytes = pdfBytes.LongLength,
                Elapsed = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            // Log detailed failure information
            LogFailure(runId, fileEventId, ex, fi, inputBytes.LongLength);
            return new GraphLibRunResult
            {
                RunId = runId,
                Success = false,
                Summary = $"FAIL file='{fileName}' ({ex.GetType().Name})",
                InputBytes = inputBytes.LongLength,
                PdfBytes = pdfBytes.LongLength,
                Elapsed = sw.Elapsed
            };
        }
        finally
        {
            // Always update database records (even if exception occurred)
            var ended = DateTimeOffset.UtcNow;
            _files.UpdateFileFinished(fileEventId, ended, success, driveId, tempItemId, pdfItemId);

            _runs.UpdateRunFinished(
                runId,
                ended,
                success,
                total: 1,
                succeeded: success ? 1 : 0,
                failed: success ? 0 : 1,
                totalInputBytes: inputBytes.LongLength,
                totalPdfBytes: pdfBytes.LongLength
            );
        }
    }

    /// <summary>
    /// Logs a failure with exception details and context.
    /// Attempts to guess the pipeline stage from exception message.
    /// </summary>
    private void LogFailure(string runId, long fileEventId, Exception ex, FileInfo fi, long sizeBytes)
    {
        // Try to determine which stage failed from exception message
        string stageGuess = ex is GraphRequestException gre
            ? GuessStageFromMessage(ex.Message)
            : "unknown";

        // Build rich error payload with Graph-specific details if available
        var payload = new
        {
            runId,
            stage = stageGuess,
            success = false,
            exceptionType = ex.GetType().FullName,
            message = ex.Message,
            // Include Graph API details if this was a GraphRequestException
            graph = ex is GraphRequestException g
                ? new { statusCode = (int)g.StatusCode, requestId = g.RequestId, clientRequestId = g.ClientRequestId, responseBody = Truncate(g.ResponseBody, 2000) }
                : null,
            file = new { path = fi.FullName, name = fi.Name, extension = fi.Extension.TrimStart('.'), sizeBytes }
        };

        _logs.Insert(runId, fileEventId, DateTimeOffset.UtcNow, LogLevel.Error, stageGuess, PipelineEvents.BuildPayloadJson(payload));
    }

    /// <summary>
    /// Attempts to infer which pipeline stage failed from exception message.
    /// Used for logging when stage tracking is lost.
    /// </summary>
    private static string GuessStageFromMessage(string msg)
    {
        var m = msg.ToLowerInvariant();
        if (m.Contains("resolvesite")) return GraphStage.ResolveSite;
        if (m.Contains("resolvedrive")) return GraphStage.ResolveDrive;
        if (m.Contains("ensurefolder")) return GraphStage.EnsureFolder;
        if (m.Contains("upload")) return GraphStage.Upload;
        if (m.Contains("convert")) return GraphStage.Convert;
        if (m.Contains("cleanup")) return GraphStage.Cleanup;
        return "unknown";
    }

    /// <summary>
    /// Conditionally logs an event.
    /// If logFailuresOnly is true, skips logging successful operations.
    /// </summary>
    private void MaybeLog(bool logFailuresOnly, bool success, string runId, long fileEventId, string level, string stage, object payload)
    {
        if (logFailuresOnly && success) return; // Skip success logs if configured
        _logs.Insert(runId, fileEventId, DateTimeOffset.UtcNow, level, stage, PipelineEvents.BuildPayloadJson(payload));
    }

    /// <summary>
    /// Truncates a string to maximum length, appending "...(truncated)" if needed.
    /// Used to limit large error response bodies in logs.
    /// </summary>
    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length <= max) return s;
        return s.Substring(0, max) + "...(truncated)";
    }
}

```

## src\GraphLib.Core\Pipeline\PipelineEvents.cs

```cs
using System.Text.Json;

namespace GraphLib.Core.Pipeline;

/// <summary>
/// Helper class for serializing pipeline events to JSON for storage in EventLogs table.
/// </summary>
public static class PipelineEvents
{
    /// <summary>
    /// Converts an object to a compact JSON string (no indentation).
    /// Used for payload serialization in EventLogRepository.Insert().
    /// </summary>
    public static string BuildPayloadJson(object o)
        => JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = false });
}

```

## src\GraphLib.Core\Data\Repositories\SettingsRepository.cs

```cs
using GraphLib.Core.Models;
using GraphLib.Core.Secrets;
using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

/// <summary>
/// Manages AppSettings table in SQLite.
/// Loads application configuration from the database (row Id=1).
/// Integrates with ISecretProvider for secret resolution/decryption.
/// </summary>
public sealed class SettingsRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ISecretProvider _secretProvider;

    public SettingsRepository(DbConnectionFactory factory, ISecretProvider secretProvider)
    {
        _factory = factory;
        _secretProvider = secretProvider;
    }

    /// <summary>
    /// Loads AppSettings from the database (Id=1).
    /// </summary>
    /// <returns>Populated GraphLibSettings object</returns>
    public GraphLibSettings Get()
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT
  SiteUrl, LibraryName, TempFolder, PdfFolder,
  CleanupTemp, ConflictBehavior,
  StorePdfInSharePoint, ProcessFolderMode, IgnoreFailuresWhenFolderMode,
  TenantId, ClientId, ClientSecret
FROM AppSettings
WHERE Id = 1;
";
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            throw new InvalidOperationException("No AppSettings row found. Run `graphlib init` first.");

        // Parse conflict behavior string to enum
        var conflict = ConflictBehaviorExtensions.Parse(r.GetString(5), ConflictBehavior.Replace);

        // Get raw secret from DB and let ISecretProvider decrypt/resolve it
        var rawSecret = r.GetString(11);
        var resolvedSecret = _secretProvider.GetSecret("ClientSecret", rawSecret);

        // Map database columns to settings object
        // Note: SQLite stores booleans as integers (0/1), so use != 0 to convert
        return new GraphLibSettings
        {
            SiteUrl = r.GetString(0),
            LibraryName = r.GetString(1),
            TempFolder = r.GetString(2),
            PdfFolder = r.GetString(3),

            CleanupTemp = r.GetInt32(4) != 0,
            ConflictBehavior = conflict,

            StorePdfInSharePoint = r.GetInt32(6) != 0,
            ProcessFolderMode = r.GetInt32(7) != 0,
            IgnoreFailuresWhenFolderMode = r.GetInt32(8) != 0,

            TenantId = r.GetString(9),
            ClientId = r.GetString(10),
            ClientSecret = resolvedSecret
        };
    }

    /// <summary>
    /// Updates AppSettings row (Id=1) with new values.
    /// Used when user modifies settings.
    /// </summary>
    public void Update(GraphLibSettings s)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE AppSettings SET
  SiteUrl = $SiteUrl,
  LibraryName = $LibraryName,
  TempFolder = $TempFolder,
  PdfFolder = $PdfFolder,
  CleanupTemp = $CleanupTemp,
  ConflictBehavior = $ConflictBehavior,
  StorePdfInSharePoint = $StorePdfInSharePoint,
  ProcessFolderMode = $ProcessFolderMode,
  IgnoreFailuresWhenFolderMode = $IgnoreFailuresWhenFolderMode,
  TenantId = $TenantId,
  ClientId = $ClientId,
  ClientSecret = $ClientSecret
WHERE Id = 1;
";
        cmd.Parameters.AddWithValue("$SiteUrl", s.SiteUrl);
        cmd.Parameters.AddWithValue("$LibraryName", s.LibraryName);
        cmd.Parameters.AddWithValue("$TempFolder", s.TempFolder);
        cmd.Parameters.AddWithValue("$PdfFolder", s.PdfFolder);
        cmd.Parameters.AddWithValue("$CleanupTemp", s.CleanupTemp ? 1 : 0);
        cmd.Parameters.AddWithValue("$ConflictBehavior", s.ConflictBehavior.ToGraphValue());
        cmd.Parameters.AddWithValue("$StorePdfInSharePoint", s.StorePdfInSharePoint ? 1 : 0);
        cmd.Parameters.AddWithValue("$ProcessFolderMode", s.ProcessFolderMode ? 1 : 0);
        cmd.Parameters.AddWithValue("$IgnoreFailuresWhenFolderMode", s.IgnoreFailuresWhenFolderMode ? 1 : 0);
        cmd.Parameters.AddWithValue("$TenantId", s.TenantId);
        cmd.Parameters.AddWithValue("$ClientId", s.ClientId);
        cmd.Parameters.AddWithValue("$ClientSecret", s.ClientSecret);
        cmd.ExecuteNonQuery();
    }
}

```

## src\GraphLib.Core\Graph\GraphAuth.cs

```cs
using Microsoft.Identity.Client;

namespace GraphLib.Core.Graph;

/// <summary>
/// Handles Azure AD authentication via Microsoft Identity Client library.
/// Uses app-only authentication (no user interaction), suitable for background services.
/// Obtains access tokens for Microsoft Graph API v1.0 calls.
/// </summary>
public sealed class GraphAuth
{
    private readonly IConfidentialClientApplication _app;

    /// <summary>
    /// Initializes auth with Azure AD credentials.
    /// </summary>
    /// <param name="tenantId">Azure AD tenant GUID</param>
    /// <param name="clientId">Azure AD app registration ID (GUID)</param>
    /// <param name="clientSecret">App registration client secret</param>
    public GraphAuth(string tenantId, string clientId, string clientSecret)
    {
        _app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .Build();
    }

    /// <summary>
    /// Acquires an access token for Microsoft Graph API calls.
    /// The token is valid for ~1 hour and is cached by MSAL.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var result = await _app
            .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
            .ExecuteAsync(ct);

        return result.AccessToken;
    }
}

```

## src\GraphLib.Core\Graph\GraphClient.cs

```cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GraphLib.Core.Graph;

/// <summary>
/// HTTP client wrapper for Microsoft Graph API v1.0 endpoint.
/// Automatically injects Bearer token and request tracking headers.
/// All other Graph*Service classes use this client.
/// </summary>
public sealed class GraphClient
{
    private readonly HttpClient _http;
    private readonly GraphAuth _auth;

    /// <summary>
    /// Initializes the Graph client with authentication.
    /// Sets base address to https://graph.microsoft.com/v1.0/
    /// </summary>
    public GraphClient(HttpClient http, GraphAuth auth)
    {
        _http = http;
        _auth = auth;
        _http.BaseAddress ??= new Uri("https://graph.microsoft.com/v1.0/");
    }

    /// <summary>
    /// Sends an HTTP request to Graph API with authorization token and client request ID.
    /// </summary>
    /// <param name="req">The HTTP request to send</param>
    /// <param name="clientRequestId">Optional request ID for tracking (used in request headers)</param>
    /// <param name="ct">Cancellation token for async operation</param>
    /// <returns>HTTP response from Graph API</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string? clientRequestId, CancellationToken ct)
    {
        // Get fresh token and add to Authorization header
        var token = await _auth.GetAccessTokenAsync(ct);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Add request tracking headers for debugging (optional)
        if (!string.IsNullOrWhiteSpace(clientRequestId))
        {
            req.Headers.TryAddWithoutValidation("client-request-id", clientRequestId);
            req.Headers.TryAddWithoutValidation("return-client-request-id", "true");
        }

        return await _http.SendAsync(req, ct);
    }

    /// <summary>
    /// Safely reads response body as string, returns empty string on error.
    /// Used by Graph services to avoid exception bubbling on read failures.
    /// </summary>
    public static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await resp.Content.ReadAsStringAsync(ct); }
        catch { return ""; }
    }

    /// <summary>
    /// Creates a JSON-encoded HTTP content for request body.
    /// Serializes any object to JSON with UTF-8 encoding.
    /// </summary>
    public static StringContent JsonBody(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");
}

```
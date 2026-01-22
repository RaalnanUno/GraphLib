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

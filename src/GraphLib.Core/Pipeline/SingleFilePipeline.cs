using System.Diagnostics;
using GraphLib.Core.Data.Repositories;
using GraphLib.Core.Graph;
using GraphLib.Core.Models;

namespace GraphLib.Core.Pipeline;

public sealed class SingleFilePipeline
{
    private readonly GraphSiteResolver _siteResolver;
    private readonly GraphDriveResolver _driveResolver;
    private readonly GraphFolderService _folders;
    private readonly GraphUploadService _upload;
    private readonly GraphPdfConversionService _convert;
    private readonly GraphUploadService _storePdf;
    private readonly GraphCleanupService _cleanup;

    private readonly RunRepository _runs;
    private readonly FileEventRepository _files;
    private readonly EventLogRepository _logs;

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

    public async Task<GraphLibRunResult> RunAsync(
        string runId,
        string filePath,
        GraphLibSettings settings,
        bool logFailuresOnly,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var started = DateTimeOffset.UtcNow;

        _runs.InsertRunStarted(runId, started);

        var fi = new FileInfo(filePath);
        if (!fi.Exists) throw new FileNotFoundException("Input file not found.", filePath);

        var fileName = fi.Name;
        var ext = fi.Extension.TrimStart('.').ToLowerInvariant();
        var inputBytes = await File.ReadAllBytesAsync(fi.FullName, ct);

        var fileEventId = _files.InsertFileStarted(runId, fi.FullName, fileName, ext, inputBytes.LongLength, DateTimeOffset.UtcNow);

        string? driveId = null;
        string? tempItemId = null;
        string? pdfItemId = null;
        byte[] pdfBytes = Array.Empty<byte>();

        bool success = false;

        try
        {
            var clientReq = Guid.NewGuid().ToString();

            // resolve site
            var (siteId, _) = await _siteResolver.ResolveSiteAsync(settings.SiteUrl, clientReq, ct);
            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.ResolveSite, new
            {
                runId, stage = GraphStage.ResolveSite, success = true, siteId, file = new { path = fi.FullName, name = fileName, extension = ext, sizeBytes = inputBytes.LongLength }
            });

            // resolve drive
            (driveId, _) = await _driveResolver.ResolveDriveAsync(siteId, settings.LibraryName, clientReq, ct);
            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.ResolveDrive, new
            {
                runId, stage = GraphStage.ResolveDrive, success = true, siteId, driveId, libraryName = settings.LibraryName
            });

            // ensure folders
            await _folders.EnsureFolderAsync(driveId, settings.TempFolder, clientReq, ct);
            if (settings.StorePdfInSharePoint && !string.IsNullOrWhiteSpace(settings.PdfFolder))
                await _folders.EnsureFolderAsync(driveId, settings.PdfFolder, clientReq, ct);

            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.EnsureFolder, new
            {
                runId, stage = GraphStage.EnsureFolder, success = true, tempFolder = settings.TempFolder, pdfFolder = settings.PdfFolder
            });

            // upload to temp
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

            // convert (download)
            pdfBytes = await _convert.DownloadPdfAsync(driveId, tempItemId, clientReq, ct);

            MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.Convert, new
            {
                runId, stage = GraphStage.Convert, success = true, driveId, tempItemId, pdfBytes = pdfBytes.LongLength
            });

            // store pdf (optional)
            if (settings.StorePdfInSharePoint && !string.IsNullOrWhiteSpace(settings.PdfFolder))
            {
                var pdfName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";

                (pdfItemId, _) = await _storePdf.UploadToFolderAsync(
                    driveId,
                    settings.PdfFolder,
                    pdfName,
                    pdfBytes,
                    settings.ConflictBehavior, // v1 respects setting; folder-mode later can force replace
                    clientReq,
                    ct);

                MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.StorePdf, new
                {
                    runId, stage = GraphStage.StorePdf, success = true, driveId, pdfItemId, pdfName, pdfFolder = settings.PdfFolder
                });
            }

            // cleanup (optional)
            if (settings.CleanupTemp && tempItemId is not null)
            {
                await _cleanup.DeleteItemAsync(driveId, tempItemId, clientReq, ct);

                MaybeLog(logFailuresOnly, true, runId, fileEventId, LogLevel.Info, GraphStage.Cleanup, new
                {
                    runId, stage = GraphStage.Cleanup, success = true, driveId, tempItemId
                });
            }

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

    private void LogFailure(string runId, long fileEventId, Exception ex, FileInfo fi, long sizeBytes)
    {
        string stageGuess = ex is GraphRequestException gre
            ? GuessStageFromMessage(ex.Message)
            : "unknown";

        var payload = new
        {
            runId,
            stage = stageGuess,
            success = false,
            exceptionType = ex.GetType().FullName,
            message = ex.Message,
            graph = ex is GraphRequestException g
                ? new { statusCode = (int)g.StatusCode, requestId = g.RequestId, clientRequestId = g.ClientRequestId, responseBody = Truncate(g.ResponseBody, 2000) }
                : null,
            file = new { path = fi.FullName, name = fi.Name, extension = fi.Extension.TrimStart('.'), sizeBytes }
        };

        _logs.Insert(runId, fileEventId, DateTimeOffset.UtcNow, LogLevel.Error, stageGuess, PipelineEvents.BuildPayloadJson(payload));
    }

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

    private void MaybeLog(bool logFailuresOnly, bool success, string runId, long fileEventId, string level, string stage, object payload)
    {
        if (logFailuresOnly && success) return;
        _logs.Insert(runId, fileEventId, DateTimeOffset.UtcNow, level, stage, PipelineEvents.BuildPayloadJson(payload));
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length <= max) return s;
        return s.Substring(0, max) + "...(truncated)";
    }
}

I am getting a `Forbidden` error in `SendForJsonAsync` when it is called from `UploadToFolderAsync`. So far, no other errors appear.
 I suspect it may be a permissions issue, but I want to be sure before I bounce it back to the Server Monkeys.

```cs
// File: GraphLibPdfRunner.cs
// Target: .NET Framework 4.8
//
// NuGet packages typically needed:
// - Microsoft.Identity.Client (MSAL)
// - Microsoft.Data.Sqlite (optional; only if you enable SQLite error logging)
//
// Notes:
// - This is a "single-class" runner for the PDF-conversion slice of GraphLib.
// - It’s async-first (no sync wrappers).
// - It does NOT store credentials anywhere.
// - Optional SQLite logging is silent + non-blocking: if it fails, GraphLib still runs.
//
// -----------------------------------------------------------------------------
// USAGE EXAMPLE (minimal):
// -----------------------------------------------------------------------------
// var settings = new GraphLibSettings
// {
//     TenantId = "{tenant-guid}",
//     ClientId = "{app-guid}",
//     ClientSecret = "{secret}",
//     SiteUrl = "https://contoso.sharepoint.com/sites/MySite",
//     LibraryName = "Documents",
//     TempFolder = "_graphlib-temp",
//     ConflictBehavior = ConflictBehavior.Replace
// };
//
// var runner = new GraphLibPdfRunner(settings, new GraphLibSqliteErrorLogger()); // logger optional
// var result = await runner.ConvertFileToPdfAsync(@"C:\docs\example.docx", CancellationToken.None);
//
// if (result.Success)
// {
//     File.WriteAllBytes(@"C:\docs\example.pdf", result.PdfBytes);
// }
//
// // Optional: surface lightweight logs without the DB pipeline.
// foreach (var log in result.Logs) Console.WriteLine($"{log.Utc:u} {log.Level} {log.Stage} {log.Message}");
//
// -----------------------------------------------------------------------------
// USAGE EXAMPLE (download-only, already have drive/item):
// -----------------------------------------------------------------------------
// var pdfBytes = await runner.DownloadPdfAsync(driveId, itemId, CancellationToken.None);
// -----------------------------------------------------------------------------

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// MSAL
using Microsoft.Identity.Client;

// Optional SQLite logger package:
// using Microsoft.Data.Sqlite;

namespace GraphLib.Core
{
    /// <summary>
    /// GraphLib "PDF conversion only" runner:
    /// - Resolve Site -> Resolve Drive -> Ensure Temp Folder -> Upload File -> Download PDF
    ///
    /// This is the “core functions in one class” version intended for .NET Framework 4.8 apps.
    /// </summary>
    public sealed class GraphLibPdfRunner
    {
        private readonly GraphLibSettings _settings;
        private readonly IGraphLibErrorLogger? _errorLogger;

        // HttpClient should be reused; allow injection for host apps.
        private readonly HttpClient _http;
        private readonly IConfidentialClientApplication _msalApp;

        /// <summary>
        /// Create a PDF runner with required settings.
        /// </summary>
        /// <param name="settings">Graph + SharePoint settings (app-only).</param>
        /// <param name="errorLogger">
        /// Optional silent logger (ex: SQLite). Never throws outward; failures are swallowed.
        /// </param>
        /// <param name="httpClient">
        /// Optional HttpClient. If null, runner creates its own HttpClient (recommended: inject a singleton).
        /// </param>
        public GraphLibPdfRunner(
            GraphLibSettings settings,
            IGraphLibErrorLogger? errorLogger = null,
            HttpClient? httpClient = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _settings = settings;
            _errorLogger = errorLogger;

            _http = httpClient ?? new HttpClient();
            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");

            _msalApp = ConfidentialClientApplicationBuilder
                .Create(_settings.ClientId)
                .WithClientSecret(_settings.ClientSecret)
                .WithAuthority(AzureCloudInstance.AzurePublic, _settings.TenantId)
                .Build();
        }

        /// <summary>
        /// Converts a local file to PDF using Microsoft Graph + SharePoint.
        /// Returns a GraphLibRunResult with PdfBytes and lightweight Logs.
        /// </summary>
        public async Task<GraphLibRunResult> ConvertFileToPdfAsync(string filePath, CancellationToken ct)
        {
            var result = new GraphLibRunResult
            {
                RunId = Guid.NewGuid().ToString("D"),
                StartedUtc = DateTimeOffset.UtcNow
            };

            var sw = Stopwatch.StartNew();

            if (string.IsNullOrWhiteSpace(filePath))
                return FailEarly(result, GraphStage.ValidateInput, "Input file path was empty.", sw);

            FileInfo fi;
            try
            {
                fi = new FileInfo(filePath);
            }
            catch (Exception ex)
            {
                return FailEarly(result, GraphStage.ValidateInput, "Invalid file path.", sw, ex);
            }

            if (!fi.Exists)
                return FailEarly(result, GraphStage.ValidateInput, "Input file does not exist.", sw);

            byte[] inputBytes;
            try
            {
                result.AddLog(LogLevel.Info, GraphStage.ReadInput, $"Reading input file '{fi.FullName}'.");
                inputBytes = await ReadAllBytesAsync(fi.FullName, ct).ConfigureAwait(false);
                result.InputBytes = inputBytes.LongLength;
            }
            catch (Exception ex)
            {
                return await FailAsync(result, GraphStage.ReadInput, "Failed reading input file.", sw, ex, ct).ConfigureAwait(false);
            }

            // One client-request-id across the run helps correlate Graph calls.
            var clientRequestId = Guid.NewGuid().ToString("D");

            string siteId;
            string driveId;
            string tempFolderPath = _settings.TempFolder ?? "_graphlib-temp";
            string itemId;

            try
            {
                // STAGE: Resolve Site
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, $"Resolving site from '{_settings.SiteUrl}'.");
                siteId = await ResolveSiteIdAsync(_settings.SiteUrl, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, $"Resolved siteId='{siteId}'.");

                // STAGE: Resolve Drive
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, $"Resolving drive (library) '{_settings.LibraryName}'.");
                driveId = await ResolveDriveIdAsync(siteId, _settings.LibraryName, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, $"Resolved driveId='{driveId}'.");

                // STAGE: Ensure Temp Folder
                result.AddLog(LogLevel.Info, GraphStage.EnsureFolder, $"Ensuring temp folder '{tempFolderPath}'.");
                await EnsureFolderAsync(driveId, tempFolderPath, clientRequestId, ct).ConfigureAwait(false);

                // STAGE: Upload
                result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploading '{fi.Name}' to '{tempFolderPath}'.");
                itemId = await UploadToFolderAsync(
                    driveId,
                    tempFolderPath,
                    fi.Name,
                    inputBytes,
                    _settings.ConflictBehavior,
                    clientRequestId,
                    ct).ConfigureAwait(false);

                result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploaded. itemId='{itemId}'.");

                // STAGE: Convert/Download PDF
                result.AddLog(LogLevel.Info, GraphStage.Convert, "Downloading PDF bytes from Graph (?format=pdf).");
                var pdfBytes = await DownloadPdfAsync(driveId, itemId, clientRequestId, ct).ConfigureAwait(false);

                result.PdfBytes = pdfBytes;
                result.PdfBytesLength = pdfBytes.LongLength;

                result.Success = true;
                result.Summary = $"OK file='{fi.Name}' pdfBytes={result.PdfBytesLength}";
                return Finish(result, sw);
            }
            catch (GraphRequestException gre)
            {
                // Graph-specific failure (status codes, request IDs, response body)
                var msg = $"Graph request failed ({(int)gre.StatusCode}) during '{gre.Stage}'.";
                return await FailAsync(result, gre.Stage, msg, sw, gre, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(result, GraphStage.Unknown, "Unhandled failure during conversion.", sw, ex, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Download PDF bytes from Graph for an existing drive/item.
        /// </summary>
        public Task<byte[]> DownloadPdfAsync(string driveId, string itemId, CancellationToken ct)
            => DownloadPdfAsync(driveId, itemId, clientRequestId: null, ct);

        // -----------------------------
        // Core Graph Steps (private)
        // -----------------------------

        private async Task<string> ResolveSiteIdAsync(string siteUrl, string clientRequestId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(siteUrl))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.BadRequest, "SiteUrl is empty.", null, null, null);

            if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.BadRequest, "SiteUrl is not a valid absolute URL.", null, null, null);

            // Graph syntax: /sites/{hostname}:/{server-relative-path}
            // Example: https://contoso.sharepoint.com/sites/MySite
            // => /sites/contoso.sharepoint.com:/sites/MySite?$select=id
            var host = uri.Host;
            var serverRelative = uri.AbsolutePath; // includes leading "/"
            if (string.IsNullOrWhiteSpace(serverRelative) || serverRelative == "/")
                serverRelative = "/"; // root site

            var path = $"sites/{host}:/{serverRelative.TrimStart('/')}?$select=id";

            var json = await GetJsonAsync(path, clientRequestId, GraphStage.ResolveSite, ct).ConfigureAwait(false);
            var id = JsonPickString(json, "id");
            if (string.IsNullOrWhiteSpace(id))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.NotFound, "Graph did not return a site id.", null, clientRequestId, json);

            return id!;
        }

        private async Task<string> ResolveDriveIdAsync(string siteId, string libraryName, string clientRequestId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(siteId))
                throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.BadRequest, "siteId is empty.", null, clientRequestId, null);

            if (string.IsNullOrWhiteSpace(libraryName))
                throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.BadRequest, "LibraryName is empty.", null, clientRequestId, null);

            // /sites/{siteId}/drives
            var json = await GetJsonAsync($"sites/{siteId}/drives?$select=id,name", clientRequestId, GraphStage.ResolveDrive, ct).ConfigureAwait(false);

            // super-light parse: find a drive with "name":"{libraryName}" and pick its "id".
            // (We intentionally avoid heavy JSON libraries for a drop-in .NET 4.8 class.)
            var driveId = JsonFindDriveIdByName(json, libraryName);
            if (string.IsNullOrWhiteSpace(driveId))
                throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.NotFound, $"No drive found with name '{libraryName}'.", null, clientRequestId, json);

            return driveId!;
        }

        private async Task EnsureFolderAsync(string driveId, string folderPath, string clientRequestId, CancellationToken ct)
        {
            // We can "ensure" by attempting to create each segment under root.
            // Graph: POST /drives/{driveId}/root/children
            // Body: { "name": "segment", "folder": { }, "@microsoft.graph.conflictBehavior": "fail" }
            var segments = SplitFolder(folderPath);
            var currentItemPath = ""; // relative under root

            foreach (var seg in segments)
            {
                ct.ThrowIfCancellationRequested();

                currentItemPath = CombineGraphPath(currentItemPath, seg);

                // Check if exists via GET /root:/{path}
                var checkUrl = $"drives/{driveId}/root:/{EscapePath(currentItemPath)}?$select=id,name,folder";
                var exists = await TryGetAsync(checkUrl, clientRequestId, GraphStage.EnsureFolder, ct).ConfigureAwait(false);
                if (exists.Exists) continue;

                // Create folder under its parent
                var parentPath = ParentPath(currentItemPath);
                var createUnder = string.IsNullOrEmpty(parentPath)
                    ? $"drives/{driveId}/root/children"
                    : $"drives/{driveId}/root:/{EscapePath(parentPath)}:/children";

                var body = "{"
                         + $"\"name\":\"{JsonEscape(seg)}\","
                         + "\"folder\":{},"
                         + "\"@microsoft.graph.conflictBehavior\":\"fail\""
                         + "}";

                await PostJsonAsync(createUnder, body, clientRequestId, GraphStage.EnsureFolder, ct).ConfigureAwait(false);
            }
        }

        private async Task<string> UploadToFolderAsync(
            string driveId,
            string folderPath,
            string fileName,
            byte[] bytes,
            ConflictBehavior conflict,
            string clientRequestId,
            CancellationToken ct)
        {
            // Simple upload:
            // PUT /drives/{driveId}/root:/{folderPath}/{fileName}:/content?@microsoft.graph.conflictBehavior=replace|rename|fail
            var conflictValue = conflict.ToGraphValue();
            var rel = CombineGraphPath(folderPath, fileName);

            var url = $"drives/{driveId}/root:/{EscapePath(rel)}:/content?@microsoft.graph.conflictBehavior={Uri.EscapeDataString(conflictValue)}";

            using (var req = new HttpRequestMessage(HttpMethod.Put, url))
            {
                req.Content = new ByteArrayContent(bytes);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var respJson = await SendForJsonAsync(req, clientRequestId, GraphStage.Upload, ct).ConfigureAwait(false);
                var itemId = JsonPickString(respJson, "id");
                if (string.IsNullOrWhiteSpace(itemId))
                    throw new GraphRequestException(GraphStage.Upload, HttpStatusCode.InternalServerError, "Upload succeeded but item id was missing.", null, clientRequestId, respJson);

                return itemId!;
            }
        }

        private async Task<byte[]> DownloadPdfAsync(string driveId, string itemId, string? clientRequestId, CancellationToken ct)
        {
            // GET /drives/{driveId}/items/{itemId}/content?format=pdf
            var url = $"drives/{driveId}/items/{itemId}/content?format=pdf";

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await ReadStringSafeAsync(resp, ct).ConfigureAwait(false);
                    throw CreateGraphError(GraphStage.Convert, resp.StatusCode, "PDF download failed.", resp, clientRequestId, body);
                }

                return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        // -----------------------------
        // HTTP / Auth helpers
        // -----------------------------

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string? clientRequestId, CancellationToken ct)
        {
            var token = await AcquireTokenAsync(ct).ConfigureAwait(false);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (!string.IsNullOrWhiteSpace(clientRequestId))
            {
                req.Headers.TryAddWithoutValidation("client-request-id", clientRequestId);
                req.Headers.TryAddWithoutValidation("return-client-request-id", "true");
            }

            return await _http.SendAsync(req, ct).ConfigureAwait(false);
        }

        private async Task<string> AcquireTokenAsync(CancellationToken ct)
        {
            var res = await _msalApp
                .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
                .ExecuteAsync(ct)
                .ConfigureAwait(false);

            return res.AccessToken;
        }

        private async Task<string> GetJsonAsync(string relativeUrl, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                return await SendForJsonAsync(req, clientRequestId, stage, ct).ConfigureAwait(false);
            }
        }

        private async Task<(bool Exists, string? Body)> TryGetAsync(string relativeUrl, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return (true, await ReadStringSafeAsync(resp, ct).ConfigureAwait(false));

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return (false, null);

                var body = await ReadStringSafeAsync(resp, ct).ConfigureAwait(false);
                throw CreateGraphError(stage, resp.StatusCode, "GET failed.", resp, clientRequestId, body);
            }
        }

        private async Task<string> PostJsonAsync(string relativeUrl, string jsonBody, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, relativeUrl))
            {
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                return await SendForJsonAsync(req, clientRequestId, stage, ct).ConfigureAwait(false);
            }
        }

        private async Task<string> SendForJsonAsync(HttpRequestMessage req, string clientRequestId, string stage, CancellationToken ct)
        {
            var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);
            var body = await ReadStringSafeAsync(resp, ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                throw CreateGraphError(stage, resp.StatusCode, "Graph request failed.", resp, clientRequestId, body);

            return body;
        }

        private static GraphRequestException CreateGraphError(
            string stage,
            HttpStatusCode statusCode,
            string message,
            HttpResponseMessage? resp,
            string? clientRequestId,
            string? responseBody)
        {
            var requestId = TryGetHeader(resp, "request-id") ?? TryGetHeader(resp, "x-ms-ags-diagnostic");
            return new GraphRequestException(stage, statusCode, message, requestId, clientRequestId, responseBody);
        }

        private static string? TryGetHeader(HttpResponseMessage? resp, string name)
        {
            if (resp == null) return null;
            if (!resp.Headers.TryGetValues(name, out var values)) return null;
            foreach (var v in values) return v;
            return null;
        }

        private static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            try
            {
                // In .NET 4.8, ReadAsStringAsync() doesn’t accept CancellationToken.
                // We’ll just read it safely; ct cancellation is already honored for SendAsync.
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
                return "";
            }
        }

        private static Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct)
        {
            // .NET Framework 4.8 has File.ReadAllBytesAsync in newer framework updates,
            // but to be safe for “plain” net48, use Task.Run.
            return Task.Run(() => File.ReadAllBytes(path), ct);
        }

        // -----------------------------
        // Result + Logging
        // -----------------------------

        private GraphLibRunResult Finish(GraphLibRunResult r, Stopwatch sw)
        {
            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            r.AddLog(LogLevel.Info, GraphStage.Done, $"Done in {(int)r.Elapsed.TotalMilliseconds}ms.");

            return r;
        }

        private GraphLibRunResult FailEarly(GraphLibRunResult r, string stage, string message, Stopwatch sw, Exception? ex = null)
        {
            r.Success = false;
            r.Summary = "FAIL " + message;
            r.AddLog(LogLevel.Error, stage, message);

            if (ex != null)
                r.AddLog(LogLevel.Error, stage, ex.GetType().Name + ": " + ex.Message);

            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;

            // Joke for dev morale (and to appease the ancient gods of build stability):
            // If you can read this, you have already angered the printer driver spirits. Offer them a PDF.

            return r;
        }

        private async Task<GraphLibRunResult> FailAsync(GraphLibRunResult r, string stage, string message, Stopwatch sw, Exception ex, CancellationToken ct)
        {
            r.Success = false;
            r.Summary = "FAIL " + message;
            r.AddLog(LogLevel.Error, stage, message);
            r.AddLog(LogLevel.Error, stage, ex.GetType().Name + ": " + ex.Message);

            // Silent optional logger (SQLite, etc.). Never block success/failure flow.
            await SafeLogErrorAsync(r.RunId, stage, message, ex, ct).ConfigureAwait(false);

            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            return r;
        }

        private async Task SafeLogErrorAsync(string runId, string stage, string message, Exception ex, CancellationToken ct,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "")
        {
            if (_errorLogger == null) return;

            try
            {
                await _errorLogger.LogErrorAsync(
                    runId: runId,
                    stage: stage,
                    message: message,
                    exception: ex,
                    callerFilePath: callerFile,
                    callerMemberName: callerMember,
                    ct: ct).ConfigureAwait(false);
            }
            catch
            {
                // Must NEVER interfere with basic operation.
            }
        }

        // -----------------------------
        // Ultra-light JSON helpers
        // (intentionally minimal for “single file drop-in”)
        // -----------------------------

        private static string? JsonPickString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName)) return null;

            // naive: find  "propertyName":"value"
            // good enough for Graph "id" and similar shallow fields.
            var needle = "\"" + propertyName + "\"";
            var i = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;

            i = json.IndexOf(':', i);
            if (i < 0) return null;

            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

            if (i >= json.Length || json[i] != '"') return null;
            i++;

            var start = i;
            while (i < json.Length)
            {
                if (json[i] == '"' && json[i - 1] != '\\') break;
                i++;
            }

            if (i <= start) return null;
            return UnescapeJsonString(json.Substring(start, i - start));
        }

        private static string? JsonFindDriveIdByName(string json, string driveName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(driveName)) return null;

            // Very lightweight approach:
            // Find occurrences of "name":"{driveName}" and then backtrack/forward to pick nearest "id":"..."
            var target = "\"name\":\"" + JsonEscape(driveName) + "\"";
            var idx = json.IndexOf(target, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            // Search backwards a bit for "id":"..."
            var windowStart = Math.Max(0, idx - 500);
            var windowEnd = Math.Min(json.Length, idx + 500);
            var window = json.Substring(windowStart, windowEnd - windowStart);

            // Prefer id before name (Graph usually returns id then name, but not guaranteed)
            var id = JsonPickString(window, "id");
            return id;
        }

        private static string UnescapeJsonString(string s)
        {
            // Enough for Graph ids and simple strings.
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\/", "/");
        }

        private static string JsonEscape(string s)
        {
            return (s ?? "")
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        // -----------------------------
        // Path helpers
        // -----------------------------

        private static IEnumerable<string> SplitFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                yield break;

            var parts = folderPath.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
                yield return p.Trim();
        }

        private static string CombineGraphPath(string left, string right)
        {
            left = (left ?? "").Replace('\\', '/').Trim('/');
            right = (right ?? "").Replace('\\', '/').Trim('/');

            if (string.IsNullOrEmpty(left)) return right;
            if (string.IsNullOrEmpty(right)) return left;

            return left + "/" + right;
        }

        private static string ParentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            path = path.Replace('\\', '/').Trim('/');
            var i = path.LastIndexOf('/');
            if (i < 0) return "";
            return path.Substring(0, i);
        }

        private static string EscapePath(string path)
        {
            // Graph path segments should be URI escaped but keep slashes.
            if (string.IsNullOrWhiteSpace(path)) return "";
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            for (var i = 0; i < segments.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(Uri.EscapeDataString(segments[i]));
            }
            return sb.ToString();
        }
    }

    // ---------------------------------------------------------------------------------
    // Naming-convention-friendly models (lightweight net48 versions)
    // ---------------------------------------------------------------------------------

    public static class GraphStage
    {
        public const string ValidateInput = "validate_input";
        public const string ReadInput = "read_input";
        public const string ResolveSite = "resolve_site";
        public const string ResolveDrive = "resolve_drive";
        public const string EnsureFolder = "ensure_folder";
        public const string Upload = "upload";
        public const string Convert = "convert";
        public const string Done = "done";
        public const string Unknown = "unknown";
    }

    public static class LogLevel
    {
        public const string Trace = "trace";
        public const string Debug = "debug";
        public const string Info = "info";
        public const string Warn = "warn";
        public const string Error = "error";
    }

    public enum ConflictBehavior
    {
        Fail = 0,
        Replace = 1,
        Rename = 2
    }

    public static class ConflictBehaviorExtensions
    {
        public static string ToGraphValue(this ConflictBehavior b)
        {
            switch (b)
            {
                case ConflictBehavior.Fail: return "fail";
                case ConflictBehavior.Rename: return "rename";
                case ConflictBehavior.Replace:
                default:
                    return "replace";
            }
        }
    }

    public sealed class GraphLibSettings
    {
        // SharePoint targeting
        public string SiteUrl { get; set; } = "";
        public string LibraryName { get; set; } = "Documents";
        public string TempFolder { get; set; } = "_graphlib-temp";

        // Auth (app-only)
        public string TenantId { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";

        // Upload behavior
        public ConflictBehavior ConflictBehavior { get; set; } = ConflictBehavior.Replace;
    }

    public sealed class GraphLibLogEntry
    {
        public DateTimeOffset Utc { get; set; }
        public string Level { get; set; } = LogLevel.Info;
        public string Stage { get; set; } = GraphStage.Unknown;
        public string Message { get; set; } = "";
    }

    public sealed class GraphLibRunResult
    {
        public string RunId { get; set; } = Guid.NewGuid().ToString("D");
        public bool Success { get; set; }
        public string Summary { get; set; } = "";

        public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset FinishedUtc { get; set; }

        public TimeSpan Elapsed { get; set; }

        public long InputBytes { get; set; }

        // Keep both raw bytes and length; some callers prefer one or the other.
        public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
        public long PdfBytesLength { get; set; }

        /// <summary>
        /// Lightweight in-memory logs so callers can surface what happened
        /// without needing the full DB pipeline.
        /// </summary>
        public List<GraphLibLogEntry> Logs { get; } = new List<GraphLibLogEntry>();

        public void AddLog(string level, string stage, string message)
        {
            Logs.Add(new GraphLibLogEntry
            {
                Utc = DateTimeOffset.UtcNow,
                Level = level,
                Stage = stage,
                Message = message ?? ""
            });
        }
    }

    /// <summary>
    /// A Graph-specific exception that carries HTTP status, request IDs, stage, and response body.
    /// </summary>
    public sealed class GraphRequestException : Exception
    {
        public string Stage { get; }
        public HttpStatusCode StatusCode { get; }
        public string? RequestId { get; }
        public string? ClientRequestId { get; }
        public string? ResponseBody { get; }

        public GraphRequestException(
            string stage,
            HttpStatusCode statusCode,
            string message,
            string? requestId,
            string? clientRequestId,
            string? responseBody,
            Exception? inner = null)
            : base(message, inner)
        {
            Stage = stage;
            StatusCode = statusCode;
            RequestId = requestId;
            ClientRequestId = clientRequestId;
            ResponseBody = responseBody;
        }
    }

    // ---------------------------------------------------------------------------------
    // Optional: Silent SQLite error logging (separate reusable class)
    // ---------------------------------------------------------------------------------

    public interface IGraphLibErrorLogger
    {
        Task LogErrorAsync(
            string runId,
            string stage,
            string message,
            Exception exception,
            string callerFilePath,
            string callerMemberName,
            CancellationToken ct);
    }

    /// <summary>
    /// Optional "zero-setup" SQLite error logger:
    /// - Creates a db file under LocalAppData automatically.
    /// - Creates the table automatically.
    /// - Swallows ALL exceptions (never interferes with GraphLib operation).
    /// - Stores no credentials, only error metadata + stack traces.
    ///
    /// If you cannot use SQLite packages in your corp environment, just don't pass this logger.
    /// </summary>
    public sealed class GraphLibSqliteErrorLogger : IGraphLibErrorLogger
    {
        // You can change this path if you want it next to the app, but LocalAppData tends to be safer.
        private readonly string _dbPath;

        public GraphLibSqliteErrorLogger(string? dbPath = null)
        {
            _dbPath = string.IsNullOrWhiteSpace(dbPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GraphLib", "GraphLib.Errors.db")
                : dbPath!;
        }

        public async Task LogErrorAsync(
            string runId,
            string stage,
            string message,
            Exception exception,
            string callerFilePath,
            string callerMemberName,
            CancellationToken ct)
        {
            // IMPORTANT: This logger must never interfere with normal execution.
            try
            {
                EnsureFolderExists(Path.GetDirectoryName(_dbPath));

                // If you don’t want the Microsoft.Data.Sqlite dependency, remove this class entirely.
                // Requires: using Microsoft.Data.Sqlite;

                /*
                var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
                using (var conn = new SqliteConnection(cs))
                {
                    await conn.OpenAsync(ct).ConfigureAwait(false);

                    // Create table (idempotent)
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText =
@"CREATE TABLE IF NOT EXISTS ErrorLogs (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Utc TEXT NOT NULL,
  RunId TEXT,
  Stage TEXT,
  Level TEXT,
  Message TEXT,
  ExceptionType TEXT,
  ExceptionMessage TEXT,
  StackTrace TEXT,
  CallerFile TEXT,
  CallerMember TEXT
);";
                        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText =
@"INSERT INTO ErrorLogs
(Utc, RunId, Stage, Level, Message, ExceptionType, ExceptionMessage, StackTrace, CallerFile, CallerMember)
VALUES
($Utc, $RunId, $Stage, $Level, $Message, $ExceptionType, $ExceptionMessage, $StackTrace, $CallerFile, $CallerMember);";

                        cmd.Parameters.AddWithValue("$Utc", DateTimeOffset.UtcNow.ToString("o"));
                        cmd.Parameters.AddWithValue("$RunId", runId ?? "");
                        cmd.Parameters.AddWithValue("$Stage", stage ?? "");
                        cmd.Parameters.AddWithValue("$Level", LogLevel.Error);
                        cmd.Parameters.AddWithValue("$Message", message ?? "");
                        cmd.Parameters.AddWithValue("$ExceptionType", exception.GetType().FullName ?? exception.GetType().Name);
                        cmd.Parameters.AddWithValue("$ExceptionMessage", exception.Message ?? "");
                        cmd.Parameters.AddWithValue("$StackTrace", exception.ToString()); // includes stack trace
                        cmd.Parameters.AddWithValue("$CallerFile", callerFilePath ?? "");
                        cmd.Parameters.AddWithValue("$CallerMember", callerMemberName ?? "");

                        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
                */

                // If you uncomment the SQLite code above, remove this line:
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch
            {
                // Swallow everything. Logging must be "best effort".
            }
        }

        private static void EnsureFolderExists(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }
    }
}

```
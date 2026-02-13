// File: GraphLibPdfRunner.cs
// Target: .NET Framework 4.8 (C# 7.3)
//
// CHANGE NOTES (why this rewrite exists):
//  1) C# 7.3 does NOT support nullable reference types, so:
//      - removed "#nullable enable"
//      - removed "string?" / "HttpClient?" / "IGraphLibErrorLogger?" usage
//      - use classic null checks + defensive coding instead
//  2) SQLite is NOT implemented at this level:
//      - removed the SQLite logger class and the logger interface entirely
//  3) Added a "Report" object that can be serialized to JSON easily:
//      - GraphLibRunResult now includes a GraphLibReport property
//      - Report includes toggles, file paths, sharepoint IDs, metrics, and a few IDs for troubleshooting
//
// IMPORTANT DESIGN CHOICE (your request):
//  - Toggles are INTERNAL ONLY (not externally settable).
//    They are private const values in this class.

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

namespace GraphLib.Core
{
    /// <summary>
    /// GraphLib "PDF conversion only" runner:
    ///  - Resolve Site -> Resolve Drive -> Ensure Temp Folder -> Upload Source -> Download PDF bytes
    ///  - Save PDF to SharePoint next to uploaded source (optional - internal toggle)
    ///  - Save PDF to local disk next to original source (optional - internal toggle)
    ///
    /// Intended for .NET Framework 4.8 projects using C# 7.3.
    /// </summary>
    public sealed class GraphLibPdfRunner
    {
        // ---------------------------------------------------------------------
        // INTERNAL TOGGLES (NOT externally settable)
        // ---------------------------------------------------------------------
        private const bool SAVE_PDF_TO_SHAREPOINT = true;
        private const bool SAVE_PDF_TO_LOCAL_DISK = true;

        // If you later want to delete the uploaded source file after conversion, you can implement it.
        private const bool KEEP_SOURCE_IN_SHAREPOINT = true;

        // Determines PDF naming: "Report.docx" -> "Report.pdf"
        private const bool USE_SOURCE_BASENAME_FOR_PDF = true;

        // ---------------------------------------------------------------------

        private readonly GraphLibSettings _settings;

        // HttpClient should be reused; allow injection for host apps.
        private readonly HttpClient _http;
        private readonly IConfidentialClientApplication _msalApp;

        /// <summary>
        /// Create a PDF runner with required settings.
        /// </summary>
        /// <param name="settings">Graph + SharePoint settings (app-only).</param>
        /// <param name="httpClient">
        /// Optional HttpClient. If null, runner creates its own HttpClient
        /// (recommended: inject a singleton in real apps).
        /// </param>
        public GraphLibPdfRunner(
            GraphLibSettings settings,
            HttpClient httpClient = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _settings = settings;

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
        /// Returns a GraphLibRunResult with:
        ///  - PdfBytes (in memory)
        ///  - LocalPdfPath (if enabled)
        ///  - SharePointPdfItemId (if enabled)
        ///  - Report (JSON-friendly summary object)
        ///  - Logs (in-memory timeline)
        /// </summary>
        public async Task<GraphLibRunResult> ConvertFileToPdfAsync(string filePath, CancellationToken ct)
        {
            var result = new GraphLibRunResult
            {
                RunId = Guid.NewGuid().ToString("D"),
                StartedUtc = DateTimeOffset.UtcNow
            };

            // Report is created immediately so we can populate it even on early failures.
            result.Report = GraphLibReport.CreateDefault();

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

            // Populate report with local path info early
            result.Report.InputFilePath = fi.FullName;
            result.Report.InputFileName = fi.Name;
            result.Report.InputFileBytes = 0; // set after read

            byte[] inputBytes;
            try
            {
                result.AddLog(LogLevel.Info, GraphStage.ReadInput, "Reading input file from disk.");
                inputBytes = await ReadAllBytesAsync(fi.FullName, ct).ConfigureAwait(false);

                result.InputBytes = inputBytes.LongLength;
                result.Report.InputFileBytes = inputBytes.LongLength;
            }
            catch (Exception ex)
            {
                return await FailAsync(result, GraphStage.ReadInput, "Failed reading input file.", sw, ex, ct).ConfigureAwait(false);
            }

            // One client-request-id across the run helps correlate Graph calls.
            var clientRequestId = Guid.NewGuid().ToString("D");
            result.Report.ClientRequestId = clientRequestId;

            string siteId;
            string driveId;
            string tempFolderPath = string.IsNullOrWhiteSpace(_settings.TempFolder) ? "_graphlib-temp" : _settings.TempFolder;
            string uploadedSourceItemId;

            // Determine the PDF filename we will use in SharePoint + locally.
            var sourceName = fi.Name;
            var pdfFileName = USE_SOURCE_BASENAME_FOR_PDF
                ? (Path.GetFileNameWithoutExtension(sourceName) + ".pdf")
                : (sourceName + ".pdf");

            // Pre-compute local PDF path for the report (even if we skip writing it)
            var localPdfPath = Path.Combine(fi.DirectoryName ?? "", pdfFileName);
            result.Report.OutputPdfFileName = pdfFileName;
            result.Report.OutputLocalPdfPath = localPdfPath;

            // Toggles captured into report for troubleshooting
            result.Report.SavePdfToSharePoint = SAVE_PDF_TO_SHAREPOINT;
            result.Report.SavePdfToLocalDisk = SAVE_PDF_TO_LOCAL_DISK;
            result.Report.KeepSourceInSharePoint = KEEP_SOURCE_IN_SHAREPOINT;

            try
            {
                // STAGE: Resolve Site
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, "Resolving SharePoint siteId.");
                siteId = await ResolveSiteIdAsync(_settings.SiteUrl, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, "Resolved siteId.");

                result.Report.SiteId = siteId;
                result.Report.SiteUrl = _settings.SiteUrl;

                // STAGE: Resolve Drive (document library)
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, "Resolving driveId for document library.");
                driveId = await ResolveDriveIdAsync(siteId, _settings.LibraryName, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, "Resolved driveId.");

                result.Report.DriveId = driveId;
                result.Report.LibraryName = _settings.LibraryName;

                // STAGE: Ensure Temp Folder exists
                result.AddLog(LogLevel.Info, GraphStage.EnsureFolder, "Ensuring temp folder exists in SharePoint.");
                await EnsureFolderAsync(driveId, tempFolderPath, clientRequestId, ct).ConfigureAwait(false);

                result.Report.TempFolder = tempFolderPath;

                // STAGE: Upload source file
                result.AddLog(LogLevel.Info, GraphStage.Upload, "Uploading source file to SharePoint temp folder.");
                uploadedSourceItemId = await UploadToFolderAsync(
                    driveId,
                    tempFolderPath,
                    sourceName,
                    inputBytes,
                    _settings.ConflictBehavior,
                    clientRequestId,
                    ct).ConfigureAwait(false);

                result.AddLog(LogLevel.Info, GraphStage.Upload, "Uploaded source file.");

                result.Report.SharePointSourceItemId = uploadedSourceItemId;
                result.Report.SharePointSourceFileName = sourceName;

                // STAGE: Convert/Download PDF bytes
                result.AddLog(LogLevel.Info, GraphStage.Convert, "Downloading PDF bytes from Graph (?format=pdf).");
                var pdfBytes = await DownloadPdfAsync(driveId, uploadedSourceItemId, clientRequestId, ct).ConfigureAwait(false);

                result.PdfBytes = pdfBytes;
                result.PdfBytesLength = pdfBytes.LongLength;
                result.Report.OutputPdfBytes = pdfBytes.LongLength;

                // STAGE: Save PDF to SharePoint (next to source)
                if (SAVE_PDF_TO_SHAREPOINT)
                {
                    result.AddLog(LogLevel.Info, GraphStage.SavePdfSharePoint, "Saving PDF to SharePoint (same folder as source).");

                    var pdfItemId = await UploadToFolderAsync(
                        driveId,
                        tempFolderPath,
                        pdfFileName,
                        pdfBytes,
                        ConflictBehavior.Replace,
                        clientRequestId,
                        ct).ConfigureAwait(false);

                    result.SharePointPdfItemId = pdfItemId;
                    result.Report.SharePointPdfItemId = pdfItemId;
                }
                else
                {
                    result.AddLog(LogLevel.Debug, GraphStage.SavePdfSharePoint, "Skipping SharePoint PDF save (toggle off).");
                }

                // STAGE: Save PDF to local disk (next to original)
                if (SAVE_PDF_TO_LOCAL_DISK)
                {
                    result.AddLog(LogLevel.Info, GraphStage.SavePdfLocal, "Saving PDF to local disk (next to original source).");
                    await WriteAllBytesAsync(localPdfPath, pdfBytes, ct).ConfigureAwait(false);

                    result.LocalPdfPath = localPdfPath;
                    result.Report.OutputLocalPdfWritten = true;
                }
                else
                {
                    result.AddLog(LogLevel.Debug, GraphStage.SavePdfLocal, "Skipping local PDF save (toggle off).");
                    result.Report.OutputLocalPdfWritten = false;
                }

                // (Optional) If you ever implement deletion:
                if (!KEEP_SOURCE_IN_SHAREPOINT)
                {
                    // Not implemented, but report the intent so it’s not a silent surprise.
                    result.AddLog(LogLevel.Warn, GraphStage.Upload, "KEEP_SOURCE_IN_SHAREPOINT is false but delete logic is not implemented.");
                }

                result.Success = true;
                result.Summary = "OK file='" + sourceName + "' pdfBytes=" + result.PdfBytesLength;

                return Finish(result, sw);
            }
            catch (GraphRequestException gre)
            {
                // Graph-specific failure (status codes, request IDs, response body)
                var msg = "Graph request failed (" + (int)gre.StatusCode + ") during '" + gre.Stage + "'.";

                // Populate report troubleshooting info
                if (result.Report != null)
                {
                    result.Report.FailureStage = gre.Stage;
                    result.Report.HttpStatus = (int)gre.StatusCode;
                    result.Report.GraphRequestId = gre.RequestId;
                    result.Report.GraphResponseBody = gre.ResponseBody;
                }

                return await FailAsync(result, gre.Stage, msg, sw, gre, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (result.Report != null)
                {
                    result.Report.FailureStage = GraphStage.Unknown;
                    result.Report.ExceptionType = ex.GetType().FullName;
                    result.Report.ExceptionMessage = ex.Message;
                }

                return await FailAsync(result, GraphStage.Unknown, "Unhandled failure during conversion.", sw, ex, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Download PDF bytes from Graph for an existing drive/item.
        /// </summary>
        public Task<byte[]> DownloadPdfAsync(string driveId, string itemId, CancellationToken ct)
        {
            return DownloadPdfAsync(driveId, itemId, null, ct);
        }

        // -----------------------------
        // Core Graph Steps (private)
        // -----------------------------

        private async Task<string> ResolveSiteIdAsync(string siteUrl, string clientRequestId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(siteUrl))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.BadRequest, "SiteUrl is empty.", null, null, null);

            Uri uri;
            if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out uri))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.BadRequest, "SiteUrl is not a valid absolute URL.", null, null, null);

            // Graph syntax: /sites/{hostname}:/{server-relative-path}
            var host = uri.Host;
            var serverRelative = uri.AbsolutePath; // includes leading "/"
            if (string.IsNullOrWhiteSpace(serverRelative) || serverRelative == "/")
                serverRelative = "/"; // root site

            var path = "sites/" + host + ":/" + serverRelative.TrimStart('/') + "?$select=id";

            var json = await GetJsonAsync(path, clientRequestId, GraphStage.ResolveSite, ct).ConfigureAwait(false);
            var id = JsonPickString(json, "id");
            if (string.IsNullOrWhiteSpace(id))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.NotFound, "Graph did not return a site id.", null, clientRequestId, json);

            return id;
        }

        private async Task<string> ResolveDriveIdAsync(string siteId, string libraryName, string clientRequestId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(siteId))
                throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.BadRequest, "siteId is empty.", null, clientRequestId, null);

            if (string.IsNullOrWhiteSpace(libraryName))
                throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.BadRequest, "LibraryName is empty.", null, clientRequestId, null);

            // /sites/{siteId}/drives
            var json = await GetJsonAsync("sites/" + siteId + "/drives?$select=id,name", clientRequestId, GraphStage.ResolveDrive, ct).ConfigureAwait(false);

            var driveId = JsonFindDriveIdByName(json, libraryName);
            if (string.IsNullOrWhiteSpace(driveId))
                throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.NotFound, "No drive found with name '" + libraryName + "'.", null, clientRequestId, json);

            return driveId;
        }

        private async Task EnsureFolderAsync(string driveId, string folderPath, string clientRequestId, CancellationToken ct)
        {
            var segments = SplitFolder(folderPath);
            var currentItemPath = ""; // relative under root

            foreach (var seg in segments)
            {
                ct.ThrowIfCancellationRequested();

                currentItemPath = CombineGraphPath(currentItemPath, seg);

                // Check if exists via GET /root:/{path}
                var checkUrl = "drives/" + driveId + "/root:/" + EscapePath(currentItemPath) + "?$select=id,name,folder";
                var exists = await TryGetAsync(checkUrl, clientRequestId, GraphStage.EnsureFolder, ct).ConfigureAwait(false);
                if (exists.Exists) continue;

                // Create folder under its parent
                var parentPath = ParentPath(currentItemPath);
                var createUnder = string.IsNullOrEmpty(parentPath)
                    ? "drives/" + driveId + "/root/children"
                    : "drives/" + driveId + "/root:/" + EscapePath(parentPath) + ":/children";

                var body = "{"
                         + "\"name\":\"" + JsonEscape(seg) + "\","
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

            var url =
                "drives/" + driveId +
                "/root:/" + EscapePath(rel) +
                ":/content?@microsoft.graph.conflictBehavior=" + Uri.EscapeDataString(conflictValue);

            using (var req = new HttpRequestMessage(HttpMethod.Put, url))
            {
                req.Content = new ByteArrayContent(bytes);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var respJson = await SendForJsonAsync(req, clientRequestId, GraphStage.Upload, ct).ConfigureAwait(false);
                var itemId = JsonPickString(respJson, "id");
                if (string.IsNullOrWhiteSpace(itemId))
                    throw new GraphRequestException(GraphStage.Upload, HttpStatusCode.InternalServerError, "Upload succeeded but item id was missing.", null, clientRequestId, respJson);

                return itemId;
            }
        }

        private async Task<byte[]> DownloadPdfAsync(string driveId, string itemId, string clientRequestId, CancellationToken ct)
        {
            // GET /drives/{driveId}/items/{itemId}/content?format=pdf
            var url = "drives/" + driveId + "/items/" + itemId + "/content?format=pdf";

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await ReadStringSafeAsync(resp).ConfigureAwait(false);
                    throw CreateGraphError(GraphStage.Convert, resp.StatusCode, "PDF download failed.", resp, clientRequestId, body);
                }

                return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        // -----------------------------
        // HTTP / Auth helpers
        // -----------------------------

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string clientRequestId, CancellationToken ct)
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

        private async Task<TryGetResult> TryGetAsync(string relativeUrl, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return new TryGetResult(true, await ReadStringSafeAsync(resp).ConfigureAwait(false));

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return new TryGetResult(false, null);

                var body = await ReadStringSafeAsync(resp).ConfigureAwait(false);
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
            var body = await ReadStringSafeAsync(resp).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                throw CreateGraphError(stage, resp.StatusCode, "Graph request failed.", resp, clientRequestId, body);

            return body;
        }

        private static GraphRequestException CreateGraphError(
            string stage,
            HttpStatusCode statusCode,
            string message,
            HttpResponseMessage resp,
            string clientRequestId,
            string responseBody)
        {
            // request-id is commonly present; x-ms-ags-diagnostic sometimes contains helpful JSON too.
            var requestId = TryGetHeader(resp, "request-id") ?? TryGetHeader(resp, "x-ms-ags-diagnostic");
            return new GraphRequestException(stage, statusCode, message, requestId, clientRequestId, responseBody);
        }

        private static string TryGetHeader(HttpResponseMessage resp, string name)
        {
            if (resp == null) return null;
            IEnumerable<string> values;
            if (!resp.Headers.TryGetValues(name, out values)) return null;
            foreach (var v in values) return v;
            return null;
        }

        private static Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct)
        {
            // net48 safe approach: run file IO on a worker thread.
            return Task.Run(() => File.ReadAllBytes(path), ct);
        }

        private static Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct)
        {
            // net48 safe approach: run file IO on a worker thread.
            return Task.Run(() => File.WriteAllBytes(path, bytes), ct);
        }

        private static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp)
        {
            try
            {
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
                return "";
            }
        }

        // -----------------------------
        // Result + Logging
        // -----------------------------

        private GraphLibRunResult Finish(GraphLibRunResult r, Stopwatch sw)
        {
            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            r.AddLog(LogLevel.Info, GraphStage.Done, "Done in " + (int)r.Elapsed.TotalMilliseconds + "ms.");

            // Report summary fields (nice for JSON dumps)
            if (r.Report != null)
            {
                r.Report.Success = r.Success;
                r.Report.ElapsedMs = (long)r.Elapsed.TotalMilliseconds;
                r.Report.FinishedUtc = r.FinishedUtc;
            }

            return r;
        }

        private GraphLibRunResult FailEarly(GraphLibRunResult r, string stage, string message, Stopwatch sw, Exception ex = null)
        {
            r.Success = false;
            r.Summary = "FAIL " + message;
            r.AddLog(LogLevel.Error, stage, message);

            if (ex != null)
                r.AddLog(LogLevel.Error, stage, ex.GetType().Name + ": " + ex.Message);

            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;

            if (r.Report != null)
            {
                r.Report.Success = false;
                r.Report.ElapsedMs = (long)r.Elapsed.TotalMilliseconds;
                r.Report.FailureStage = stage;
                r.Report.ExceptionType = ex == null ? null : ex.GetType().FullName;
                r.Report.ExceptionMessage = ex == null ? null : ex.Message;
                r.Report.FinishedUtc = r.FinishedUtc;
            }

            return r;
        }

        private async Task<GraphLibRunResult> FailAsync(GraphLibRunResult r, string stage, string message, Stopwatch sw, Exception ex, CancellationToken ct)
        {
            r.Success = false;
            r.Summary = "FAIL " + message;
            r.AddLog(LogLevel.Error, stage, message);
            r.AddLog(LogLevel.Error, stage, ex.GetType().Name + ": " + ex.Message);

            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;

            if (r.Report != null)
            {
                r.Report.Success = false;
                r.Report.ElapsedMs = (long)r.Elapsed.TotalMilliseconds;
                r.Report.FailureStage = stage;
                r.Report.ExceptionType = ex.GetType().FullName;
                r.Report.ExceptionMessage = ex.Message;
                r.Report.FinishedUtc = r.FinishedUtc;
            }

            // No SQLite, no external logging. Keep behavior simple.
            await Task.CompletedTask.ConfigureAwait(false);
            return r;
        }

        // -----------------------------
        // Ultra-light JSON helpers
        // (intentionally minimal for “single file drop-in”)
        // -----------------------------

        private static string JsonPickString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName)) return null;

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

        private static string JsonFindDriveIdByName(string json, string driveName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(driveName)) return null;

            var target = "\"name\":\"" + JsonEscape(driveName) + "\"";
            var idx = json.IndexOf(target, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var windowStart = Math.Max(0, idx - 500);
            var windowEnd = Math.Min(json.Length, idx + 500);
            var window = json.Substring(windowStart, windowEnd - windowStart);

            var id = JsonPickString(window, "id");
            return id;
        }

        private static string UnescapeJsonString(string s)
        {
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

        // Small helper type for TryGetAsync (C# 7.3-friendly, no tuples required)
        private sealed class TryGetResult
        {
            public readonly bool Exists;
            public readonly string Body;

            public TryGetResult(bool exists, string body)
            {
                Exists = exists;
                Body = body;
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // Models (C# 7.3 friendly)
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

        public const string SavePdfSharePoint = "save_pdf_sharepoint";
        public const string SavePdfLocal = "save_pdf_local";

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
        public string SiteUrl { get; set; }
        public string LibraryName { get; set; }
        public string TempFolder { get; set; }

        // Auth (app-only)
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        // Upload behavior (source upload)
        public ConflictBehavior ConflictBehavior { get; set; }

        public GraphLibSettings()
        {
            SiteUrl = "";
            LibraryName = "Documents";
            TempFolder = "_graphlib-temp";

            TenantId = "";
            ClientId = "";
            ClientSecret = "";

            ConflictBehavior = ConflictBehavior.Replace;
        }
    }

    public sealed class GraphLibLogEntry
    {
        public DateTimeOffset Utc { get; set; }
        public string Level { get; set; }
        public string Stage { get; set; }
        public string Message { get; set; }

        public GraphLibLogEntry()
        {
            Utc = DateTimeOffset.UtcNow;
            Level = LogLevel.Info;
            Stage = GraphStage.Unknown;
            Message = "";
        }
    }

    /// <summary>
    /// JSON-friendly report object (safe to serialize).
    ///
    /// This is meant to be the thing you can dump into a table, log store,
    /// API response, etc., without having to parse "Logs" or exception types.
    /// </summary>
    public sealed class GraphLibReport
    {
        // Overall status
        public bool Success { get; set; }
        public long ElapsedMs { get; set; }
        public DateTimeOffset FinishedUtc { get; set; }

        // Inputs
        public string InputFilePath { get; set; }
        public string InputFileName { get; set; }
        public long InputFileBytes { get; set; }

        // Outputs
        public string OutputPdfFileName { get; set; }
        public long OutputPdfBytes { get; set; }

        // Local output (what you asked for)
        public string OutputLocalPdfPath { get; set; }
        public bool OutputLocalPdfWritten { get; set; }

        // SharePoint/Graph targeting
        public string SiteUrl { get; set; }
        public string LibraryName { get; set; }
        public string TempFolder { get; set; }

        // IDs (useful for troubleshooting)
        public string SiteId { get; set; }
        public string DriveId { get; set; }
        public string SharePointSourceItemId { get; set; }
        public string SharePointSourceFileName { get; set; }
        public string SharePointPdfItemId { get; set; }

        // Correlation
        public string ClientRequestId { get; set; }
        public string GraphRequestId { get; set; }       // from response headers (request-id or x-ms-ags-diagnostic)
        public int HttpStatus { get; set; }              // on failures
        public string FailureStage { get; set; }         // on failures
        public string GraphResponseBody { get; set; }    // on failures (safe text)

        // Exception summary (on unhandled failures)
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }

        // Toggles snapshot
        public bool SavePdfToSharePoint { get; set; }
        public bool SavePdfToLocalDisk { get; set; }
        public bool KeepSourceInSharePoint { get; set; }

        public static GraphLibReport CreateDefault()
        {
            return new GraphLibReport
            {
                Success = false,
                ElapsedMs = 0,
                FinishedUtc = default(DateTimeOffset),

                InputFilePath = "",
                InputFileName = "",
                InputFileBytes = 0,

                OutputPdfFileName = "",
                OutputPdfBytes = 0,

                OutputLocalPdfPath = "",
                OutputLocalPdfWritten = false,

                SiteUrl = "",
                LibraryName = "",
                TempFolder = "",

                SiteId = "",
                DriveId = "",
                SharePointSourceItemId = "",
                SharePointSourceFileName = "",
                SharePointPdfItemId = "",

                ClientRequestId = "",
                GraphRequestId = "",
                HttpStatus = 0,
                FailureStage = "",
                GraphResponseBody = "",

                ExceptionType = "",
                ExceptionMessage = "",

                SavePdfToSharePoint = false,
                SavePdfToLocalDisk = false,
                KeepSourceInSharePoint = true
            };
        }
    }

    public sealed class GraphLibRunResult
    {
        public string RunId { get; set; }
        public bool Success { get; set; }
        public string Summary { get; set; }

        public DateTimeOffset StartedUtc { get; set; }
        public DateTimeOffset FinishedUtc { get; set; }

        public TimeSpan Elapsed { get; set; }

        public long InputBytes { get; set; }

        // In-memory PDF bytes
        public byte[] PdfBytes { get; set; }
        public long PdfBytesLength { get; set; }

        // New: output locations / IDs
        public string LocalPdfPath { get; set; }
        public string SharePointPdfItemId { get; set; }

        // New: JSON-friendly report object
        public GraphLibReport Report { get; set; }

        // In-memory log timeline
        public List<GraphLibLogEntry> Logs { get; private set; }

        public GraphLibRunResult()
        {
            RunId = Guid.NewGuid().ToString("D");
            Success = false;
            Summary = "";

            StartedUtc = DateTimeOffset.UtcNow;
            FinishedUtc = default(DateTimeOffset);
            Elapsed = TimeSpan.Zero;

            InputBytes = 0;

            PdfBytes = new byte[0];
            PdfBytesLength = 0;

            LocalPdfPath = "";
            SharePointPdfItemId = "";

            Report = null; // set by runner
            Logs = new List<GraphLibLogEntry>();
        }

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
        public string Stage { get; private set; }
        public HttpStatusCode StatusCode { get; private set; }
        public string RequestId { get; private set; }
        public string ClientRequestId { get; private set; }
        public string ResponseBody { get; private set; }

        public GraphRequestException(
            string stage,
            HttpStatusCode statusCode,
            string message,
            string requestId,
            string clientRequestId,
            string responseBody,
            Exception inner = null)
            : base(message, inner)
        {
            Stage = stage;
            StatusCode = statusCode;
            RequestId = requestId;
            ClientRequestId = clientRequestId;
            ResponseBody = responseBody;
        }
    }
}

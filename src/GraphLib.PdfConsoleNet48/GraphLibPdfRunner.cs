// File: GraphLibPdfRunner.cs
// Target: .NET Framework 4.8
// Language: C# 7.3
//
// Namespace NOTE:
// - This file is in namespace: EVAuto
// - Settings + models are in: EVAUTO.Helpers (GraphLibPdfModels.cs)
//
// What this runner does:
//  1) Reads a local source file from disk
//  2) Uploads it into a SharePoint document library folder (TempFolder)
//  3) Uses Microsoft Graph to download the PDF rendition (?format=pdf)
//  4) Saves the PDF back into the SAME SharePoint folder, next to the uploaded source file (internal toggle)
//  5) ALSO saves the PDF locally, next to the original source file (internal toggle)
//
// Internal toggles:
// - You asked for toggles that are set *inside* GraphLibPdfRunner and not externally.
//   So they are private const values in this file.
//
// NuGet packages typically needed:
// - Microsoft.Identity.Client (MSAL)
//
// Notes:
// - Async-first, no sync wrappers.
// - Does NOT store credentials anywhere.
// - Logs are returned to caller in-memory.

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

// Models + settings live here (per your repo structure)
using EVAUTO.Helpers;

namespace EVAuto
{
    /// <summary>
    /// GraphLib "PDF conversion only" runner:
    /// - Resolve Site -> Resolve Drive -> Ensure Temp Folder -> Upload Source -> Download PDF
    /// - Save PDF to SharePoint next to uploaded source file
    /// - Save PDF locally next to original input file
    ///
    /// Intended for .NET Framework 4.8 applications.
    /// </summary>
    public sealed class GraphLibPdfRunner
    {
        // ---------------------------------------------------------------------
        // INTERNAL TOGGLES (NOT externally settable)
        // ---------------------------------------------------------------------

        // Save the generated PDF back into SharePoint next to the uploaded source file
        private const bool SAVE_PDF_TO_SHAREPOINT = true;

        // Save the generated PDF to local disk next to the original source file
        private const bool SAVE_PDF_TO_LOCAL_DISK = true;

        // Keep the uploaded source file in SharePoint (delete not implemented in this runner)
        private const bool KEEP_SOURCE_IN_SHAREPOINT = true;

        // Use "Report.docx" => "Report.pdf"
        private const bool USE_SOURCE_BASENAME_FOR_PDF = true;

        // ---------------------------------------------------------------------

        private readonly GraphLibSettings _settings;
        private readonly HttpClient _http;
        private readonly IConfidentialClientApplication _msalApp;

        /// <summary>
        /// Create a PDF runner with required settings.
        /// </summary>
        public GraphLibPdfRunner(GraphLibSettings settings, HttpClient httpClient = null)
        {
            if (settings == null) throw new ArgumentNullException("settings");

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
        /// - PdfBytes (in-memory)
        /// - Logs (in-memory)
        /// - Report (structured details, also serializable to JSON)
        /// </summary>
        public async Task<GraphLibRunResult> ConvertFileToPdfAsync(string filePath, CancellationToken ct)
        {
            var result = new GraphLibRunResult();
            result.RunId = Guid.NewGuid().ToString("D");
            result.StartedUtc = DateTimeOffset.UtcNow;

            // Report object (requested)
            result.Report = new GraphLibReport();
            result.Report.RunId = result.RunId;
            result.Report.StartedUtc = result.StartedUtc;

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

            // Set report input paths early
            result.Report.SourceFilePath = fi.FullName;

            byte[] inputBytes;
            try
            {
                result.AddLog(LogLevel.Info, GraphStage.ReadInput, "Reading input file '" + fi.FullName + "'.");
                inputBytes = await ReadAllBytesAsync(fi.FullName, ct).ConfigureAwait(false);
                result.InputBytes = inputBytes.LongLength;
                result.Report.InputBytes = result.InputBytes;
            }
            catch (Exception ex)
            {
                return await FailAsync(result, GraphStage.ReadInput, "Failed reading input file.", sw, ex, ct).ConfigureAwait(false);
            }

            // One client-request-id across the run helps correlate Graph calls.
            var clientRequestId = Guid.NewGuid().ToString("D");
            result.Report.ClientRequestId = clientRequestId;

            // PDF file name: Report.docx -> Report.pdf
            var sourceName = fi.Name;
            var pdfFileName = USE_SOURCE_BASENAME_FOR_PDF
                ? (Path.GetFileNameWithoutExtension(sourceName) + ".pdf")
                : (sourceName + ".pdf");

            // Local output path next to original source (requested)
            var localPdfPath = Path.Combine(fi.DirectoryName ?? string.Empty, pdfFileName);
            result.Report.LocalPdfPath = localPdfPath;

            var tempFolderPath = _settings.TempFolder ?? "_graphlib-temp";
            result.Report.TempFolder = tempFolderPath;
            result.Report.LibraryName = _settings.LibraryName;
            result.Report.SiteUrl = _settings.SiteUrl;

            try
            {
                // 1) Resolve Site
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, "Resolving site from '" + _settings.SiteUrl + "'.");
                var siteId = await ResolveSiteIdAsync(_settings.SiteUrl, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, "Resolved siteId='" + siteId + "'.");
                result.Report.SiteId = siteId;

                // 2) Resolve Drive (document library)
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, "Resolving drive (library) '" + _settings.LibraryName + "'.");
                var driveId = await ResolveDriveIdAsync(siteId, _settings.LibraryName, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, "Resolved driveId='" + driveId + "'.");
                result.Report.DriveId = driveId;

                // 3) Ensure Temp Folder
                result.AddLog(LogLevel.Info, GraphStage.EnsureFolder, "Ensuring temp folder '" + tempFolderPath + "'.");
                await EnsureFolderAsync(driveId, tempFolderPath, clientRequestId, ct).ConfigureAwait(false);

                // 4) Upload Source
                result.AddLog(LogLevel.Info, GraphStage.Upload, "Uploading '" + sourceName + "' to '" + tempFolderPath + "'.");
                var sourceItemId = await UploadToFolderAsync(
                    driveId,
                    tempFolderPath,
                    sourceName,
                    inputBytes,
                    _settings.ConflictBehavior,
                    clientRequestId,
                    ct).ConfigureAwait(false);

                result.AddLog(LogLevel.Info, GraphStage.Upload, "Uploaded source. itemId='" + sourceItemId + "'.");
                result.Report.SourceItemId = sourceItemId;
                result.Report.SourceFileName = sourceName;
                result.Report.PdfFileName = pdfFileName;

                // 5) Download PDF bytes from Graph rendition
                result.AddLog(LogLevel.Info, GraphStage.Convert, "Downloading PDF bytes from Graph (?format=pdf).");
                var pdfBytes = await DownloadPdfAsync(driveId, sourceItemId, clientRequestId, ct).ConfigureAwait(false);

                result.PdfBytes = pdfBytes;
                result.PdfBytesLength = pdfBytes.LongLength;
                result.Report.PdfBytes = result.PdfBytesLength;

                // 6) Save PDF back to SharePoint next to the uploaded source file
                if (SAVE_PDF_TO_SHAREPOINT)
                {
                    result.AddLog(LogLevel.Info, GraphStage.SavePdfSharePoint,
                        "Saving PDF to SharePoint as '" + pdfFileName + "' in '" + tempFolderPath + "'.");

                    var pdfItemId = await UploadToFolderAsync(
                        driveId,
                        tempFolderPath,
                        pdfFileName,
                        pdfBytes,
                        ConflictBehavior.Replace, // PDF should replace by default
                        clientRequestId,
                        ct).ConfigureAwait(false);

                    result.Report.SharePointPdfItemId = pdfItemId;
                    result.AddLog(LogLevel.Info, GraphStage.SavePdfSharePoint, "Saved PDF to SharePoint. pdfItemId='" + pdfItemId + "'.");
                }
                else
                {
                    result.AddLog(LogLevel.Debug, GraphStage.SavePdfSharePoint, "SAVE_PDF_TO_SHAREPOINT is false; skipping SharePoint PDF save.");
                }

                // 7) Save PDF locally next to original source file
                if (SAVE_PDF_TO_LOCAL_DISK)
                {
                    result.AddLog(LogLevel.Info, GraphStage.SavePdfLocal, "Saving PDF to local disk at '" + localPdfPath + "'.");
                    await WriteAllBytesAsync(localPdfPath, pdfBytes, ct).ConfigureAwait(false);
                    result.AddLog(LogLevel.Info, GraphStage.SavePdfLocal, "Saved PDF to local disk.");
                }
                else
                {
                    result.AddLog(LogLevel.Debug, GraphStage.SavePdfLocal, "SAVE_PDF_TO_LOCAL_DISK is false; skipping local PDF save.");
                }

                // NOTE: Source deletion not implemented
                if (!KEEP_SOURCE_IN_SHAREPOINT)
                {
                    result.AddLog(LogLevel.Warn, GraphStage.Upload,
                        "KEEP_SOURCE_IN_SHAREPOINT is false, but delete logic is not implemented in this runner.");
                }

                // success
                result.Success = true;
                result.Summary = "OK file='" + sourceName + "' pdfBytes=" + result.PdfBytesLength;

                // finalize report
                result.Report.Success = true;
                result.Report.Summary = result.Summary;

                return Finish(result, sw);
            }
            catch (GraphRequestException gre)
            {
                var msg = "Graph request failed (" + (int)gre.StatusCode + ") during '" + gre.Stage + "'.";
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
                    ? ("drives/" + driveId + "/root/children")
                    : ("drives/" + driveId + "/root:/" + EscapePath(parentPath) + ":/children");

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
            // PUT /drives/{driveId}/root:/{folderPath}/{fileName}:/content?@microsoft.graph.conflictBehavior=replace|rename|fail
            var conflictValue = conflict.ToGraphValue();
            var rel = CombineGraphPath(folderPath, fileName);

            var url = "drives/" + driveId + "/root:/" + EscapePath(rel) + ":/content?@microsoft.graph.conflictBehavior=" + Uri.EscapeDataString(conflictValue);

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

        private async Task<GraphTryGetResult> TryGetAsync(string relativeUrl, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);

                if (resp.IsSuccessStatusCode)
                    return new GraphTryGetResult(true, await ReadStringSafeAsync(resp).ConfigureAwait(false));

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return new GraphTryGetResult(false, null);

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
            // Useful headers for admins:
            // - request-id
            // - x-ms-ags-diagnostic (often contains datacenter + correlation info)
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

        private static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp)
        {
            try { return await resp.Content.ReadAsStringAsync().ConfigureAwait(false); }
            catch { return ""; }
        }

        private static Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct)
        {
            return Task.Run(() => File.ReadAllBytes(path), ct);
        }

        private static Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct)
        {
            return Task.Run(() => File.WriteAllBytes(path, bytes), ct);
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

            if (r.Report != null)
            {
                r.Report.FinishedUtc = r.FinishedUtc;
                r.Report.ElapsedMs = (long)r.Elapsed.TotalMilliseconds;
                r.Report.Success = r.Success;
                r.Report.Summary = r.Summary;

                // Keep logs in the report too (so the report can stand alone)
                r.Report.Logs = new List<GraphLibLogEntry>(r.Logs);
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
                r.Report.FinishedUtc = r.FinishedUtc;
                r.Report.ElapsedMs = (long)r.Elapsed.TotalMilliseconds;
                r.Report.Success = false;
                r.Report.Summary = r.Summary;
                r.Report.Logs = new List<GraphLibLogEntry>(r.Logs);

                if (ex != null)
                {
                    r.Report.ExceptionType = ex.GetType().FullName;
                    r.Report.ExceptionMessage = ex.Message;
                }
            }

            return r;
        }

        private async Task<GraphLibRunResult> FailAsync(GraphLibRunResult r, string stage, string message, Stopwatch sw, Exception ex, CancellationToken ct)
        {
            r.Success = false;
            r.Summary = "FAIL " + message;
            r.AddLog(LogLevel.Error, stage, message);
            r.AddLog(LogLevel.Error, stage, ex.GetType().Name + ": " + ex.Message);

            // No SQLite at this level, but we DO populate the report object.
            await Task.CompletedTask.ConfigureAwait(false);

            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;

            if (r.Report != null)
            {
                r.Report.FinishedUtc = r.FinishedUtc;
                r.Report.ElapsedMs = (long)r.Elapsed.TotalMilliseconds;
                r.Report.Success = false;
                r.Report.Summary = r.Summary;
                r.Report.Logs = new List<GraphLibLogEntry>(r.Logs);

                r.Report.ExceptionType = ex.GetType().FullName;
                r.Report.ExceptionMessage = ex.Message;
                r.Report.ExceptionStack = ex.ToString();
            }

            return r;
        }

        // -----------------------------
        // Ultra-light JSON helpers (for Graph responses)
        // -----------------------------

        private static string JsonPickString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName)) return null;

            // naive: find  "propertyName":"value"
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

            return JsonPickString(window, "id");
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
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (!string.IsNullOrWhiteSpace(p))
                    yield return p.Trim();
            }
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
    }

    /// <summary>
    /// Small helper to avoid tuples (keeps things obvious for junior devs).
    /// </summary>
    internal sealed class GraphTryGetResult
    {
        public bool Exists;
        public string Body;

        public GraphTryGetResult(bool exists, string body)
        {
            Exists = exists;
            Body = body;
        }
    }
}

Yep — here is the **entire, complete `GraphLibPdfRunner.cs` file**, with **PDF saved next to the temp upload**, fully wired and ready to drop in.

* Target: **.NET Framework 4.8**
* Behavior:

  * Upload source file → temp folder
  * Convert via Graph `?format=pdf`
  * **Upload PDF back to the same temp folder**
  * Return PDF bytes + IDs
* No external dependencies beyond MSAL
* SQLite logger remains optional and untouched

---

## `src\GraphLib.PdfConsoleNet48\GraphLibPdfRunner.cs`

```csharp
// File: GraphLibPdfRunner.cs
// Target: .NET Framework 4.8
//
// NuGet packages typically needed:
// - Microsoft.Identity.Client (MSAL)
// - Microsoft.Data.Sqlite (optional; only if you enable SQLite error logging)
//
// Notes:
// - Async-first, no sync wrappers.
// - App-only auth (client credentials).
// - Does NOT store credentials.
// - Optional SQLite logging is silent + non-blocking.
//
// Pipeline:
// Resolve Site -> Resolve Drive -> Ensure Temp Folder -> Upload File -> Download PDF
// -> Upload PDF next to temp upload
//

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
using Microsoft.Identity.Client;

namespace GraphLib.Core
{
    public sealed class GraphLibPdfRunner
    {
        private readonly GraphLibSettings _settings;
        private readonly IGraphLibErrorLogger? _errorLogger;

        private readonly HttpClient _http;
        private readonly IConfidentialClientApplication _msalApp;

        public GraphLibPdfRunner(
            GraphLibSettings settings,
            IGraphLibErrorLogger? errorLogger = null,
            HttpClient? httpClient = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

        // ---------------------------------------------------------------------
        // PUBLIC API
        // ---------------------------------------------------------------------

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
            try { fi = new FileInfo(filePath); }
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
                inputBytes = await Task.Run(() => File.ReadAllBytes(fi.FullName), ct).ConfigureAwait(false);
                result.InputBytes = inputBytes.LongLength;
            }
            catch (Exception ex)
            {
                return await FailAsync(result, GraphStage.ReadInput, "Failed reading input file.", sw, ex, ct);
            }

            var clientRequestId = Guid.NewGuid().ToString("D");
            var tempFolder = string.IsNullOrWhiteSpace(_settings.TempFolder) ? "_graphlib-temp" : _settings.TempFolder;

            try
            {
                // Resolve Site
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, $"Resolving site '{_settings.SiteUrl}'.");
                var siteId = await ResolveSiteIdAsync(_settings.SiteUrl, clientRequestId, ct);

                // Resolve Drive
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, $"Resolving library '{_settings.LibraryName}'.");
                var driveId = await ResolveDriveIdAsync(siteId, _settings.LibraryName, clientRequestId, ct);

                // Ensure Temp Folder
                result.AddLog(LogLevel.Info, GraphStage.EnsureFolder, $"Ensuring folder '{tempFolder}'.");
                await EnsureFolderAsync(driveId, tempFolder, clientRequestId, ct);

                // Upload Source
                result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploading '{fi.Name}'.");
                var sourceItemId = await UploadToFolderAsync(
                    driveId,
                    tempFolder,
                    fi.Name,
                    inputBytes,
                    _settings.ConflictBehavior,
                    clientRequestId,
                    ct);

                // Download PDF
                result.AddLog(LogLevel.Info, GraphStage.Convert, "Downloading PDF bytes.");
                var pdfBytes = await DownloadPdfAsync(driveId, sourceItemId, clientRequestId, ct);

                result.PdfBytes = pdfBytes;
                result.PdfBytesLength = pdfBytes.LongLength;

                // Upload PDF next to source
                if (_settings.SavePdfToSharePoint)
                {
                    var pdfName = BuildPdfFileName(fi.Name, _settings.PdfExtension);
                    result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploading PDF '{pdfName}'.");
                    result.PdfItemId = await UploadToFolderAsync(
                        driveId,
                        tempFolder,
                        pdfName,
                        pdfBytes,
                        _settings.ConflictBehavior,
                        clientRequestId,
                        ct);
                }

                result.Success = true;
                result.Summary = $"OK file='{fi.Name}' pdfBytes={result.PdfBytesLength}";
                return Finish(result, sw);
            }
            catch (GraphRequestException gre)
            {
                return await FailAsync(result, gre.Stage, $"Graph error {(int)gre.StatusCode}", sw, gre, ct);
            }
            catch (Exception ex)
            {
                return await FailAsync(result, GraphStage.Unknown, "Unhandled failure.", sw, ex, ct);
            }
        }

        public Task<byte[]> DownloadPdfAsync(string driveId, string itemId, CancellationToken ct)
            => DownloadPdfAsync(driveId, itemId, null, ct);

        // ---------------------------------------------------------------------
        // GRAPH HELPERS
        // ---------------------------------------------------------------------

        private async Task<string> ResolveSiteIdAsync(string siteUrl, string clientRequestId, CancellationToken ct)
        {
            var uri = new Uri(siteUrl);
            var path = $"sites/{uri.Host}:/{uri.AbsolutePath.TrimStart('/')}?$select=id";
            var json = await GetJsonAsync(path, clientRequestId, GraphStage.ResolveSite, ct);
            return JsonPickString(json, "id")
                ?? throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.NotFound, "Site not found.", null, clientRequestId, json);
        }

        private async Task<string> ResolveDriveIdAsync(string siteId, string libraryName, string clientRequestId, CancellationToken ct)
        {
            var json = await GetJsonAsync($"sites/{siteId}/drives?$select=id,name", clientRequestId, GraphStage.ResolveDrive, ct);
            return JsonFindDriveIdByName(json, libraryName)
                ?? throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.NotFound, "Library not found.", null, clientRequestId, json);
        }

        private async Task EnsureFolderAsync(string driveId, string folderPath, string clientRequestId, CancellationToken ct)
        {
            var segments = SplitFolder(folderPath);
            var current = "";

            foreach (var seg in segments)
            {
                current = CombineGraphPath(current, seg);
                var check = $"drives/{driveId}/root:/{EscapePath(current)}?$select=id";
                var exists = await TryGetAsync(check, clientRequestId, GraphStage.EnsureFolder, ct);
                if (exists.Exists) continue;

                var parent = ParentPath(current);
                var createUrl = string.IsNullOrEmpty(parent)
                    ? $"drives/{driveId}/root/children"
                    : $"drives/{driveId}/root:/{EscapePath(parent)}:/children";

                var body = $"{{\"name\":\"{JsonEscape(seg)}\",\"folder\":{{}},\"@microsoft.graph.conflictBehavior\":\"fail\"}}";
                await PostJsonAsync(createUrl, body, clientRequestId, GraphStage.EnsureFolder, ct);
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
            var rel = CombineGraphPath(folderPath, fileName);
            var url = $"drives/{driveId}/root:/{EscapePath(rel)}:/content?@microsoft.graph.conflictBehavior={conflict.ToGraphValue()}";

            using (var req = new HttpRequestMessage(HttpMethod.Put, url))
            {
                req.Content = new ByteArrayContent(bytes);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var json = await SendForJsonAsync(req, clientRequestId, GraphStage.Upload, ct);
                return JsonPickString(json, "id")
                    ?? throw new GraphRequestException(GraphStage.Upload, HttpStatusCode.InternalServerError, "Missing item id.", null, clientRequestId, json);
            }
        }

        private async Task<byte[]> DownloadPdfAsync(string driveId, string itemId, string? clientRequestId, CancellationToken ct)
        {
            var url = $"drives/{driveId}/items/{itemId}/content?format=pdf";
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                var resp = await SendAsync(req, clientRequestId, ct);
                if (!resp.IsSuccessStatusCode)
                    throw CreateGraphError(GraphStage.Convert, resp.StatusCode, "PDF download failed.", resp, clientRequestId, await ReadStringSafeAsync(resp));
                return await resp.Content.ReadAsByteArrayAsync();
            }
        }

        // ---------------------------------------------------------------------
        // HTTP / AUTH
        // ---------------------------------------------------------------------

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string? clientRequestId, CancellationToken ct)
        {
            var token = await _msalApp.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" }).ExecuteAsync(ct);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            if (!string.IsNullOrWhiteSpace(clientRequestId))
            {
                req.Headers.TryAddWithoutValidation("client-request-id", clientRequestId);
                req.Headers.TryAddWithoutValidation("return-client-request-id", "true");
            }

            return await _http.SendAsync(req, ct);
        }

        private async Task<string> GetJsonAsync(string url, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                return await SendForJsonAsync(req, clientRequestId, stage, ct);
        }

        private async Task<(bool Exists, string? Body)> TryGetAsync(string url, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                var resp = await SendAsync(req, clientRequestId, ct);
                if (resp.StatusCode == HttpStatusCode.NotFound) return (false, null);
                if (!resp.IsSuccessStatusCode)
                    throw CreateGraphError(stage, resp.StatusCode, "GET failed.", resp, clientRequestId, await ReadStringSafeAsync(resp));
                return (true, await ReadStringSafeAsync(resp));
            }
        }

        private async Task<string> PostJsonAsync(string url, string body, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return await SendForJsonAsync(req, clientRequestId, stage, ct);
            }
        }

        private async Task<string> SendForJsonAsync(HttpRequestMessage req, string clientRequestId, string stage, CancellationToken ct)
        {
            var resp = await SendAsync(req, clientRequestId, ct);
            var body = await ReadStringSafeAsync(resp);
            if (!resp.IsSuccessStatusCode)
                throw CreateGraphError(stage, resp.StatusCode, "Graph call failed.", resp, clientRequestId, body);
            return body;
        }

        // ---------------------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------------------

        private static string BuildPdfFileName(string source, string ext)
            => Path.GetFileNameWithoutExtension(source) + (string.IsNullOrWhiteSpace(ext) ? ".pdf" : ext);

        private static string CombineGraphPath(string left, string right)
            => string.IsNullOrEmpty(left) ? right : $"{left.TrimEnd('/')}/{right.TrimStart('/')}";

        private static IEnumerable<string> SplitFolder(string path)
            => (path ?? "").Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        private static string ParentPath(string path)
        {
            var i = path.LastIndexOf('/');
            return i < 0 ? "" : path.Substring(0, i);
        }

        private static string EscapePath(string path)
        {
            var parts = path.Split('/');
            var sb = new StringBuilder();
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(Uri.EscapeDataString(parts[i]));
            }
            return sb.ToString();
        }

        private static string? JsonPickString(string json, string name)
        {
            var key = $"\"{name}\"";
            var i = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i = json.IndexOf(':', i) + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (json[i] != '"') return null;
            i++;
            var start = i;
            while (i < json.Length && (json[i] != '"' || json[i - 1] == '\\')) i++;
            return json.Substring(start, i - start).Replace("\\\"", "\"");
        }

        private static string? JsonFindDriveIdByName(string json, string name)
        {
            var idx = json.IndexOf($"\"name\":\"{JsonEscape(name)}\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var window = json.Substring(Math.Max(0, idx - 500), Math.Min(json.Length - idx + 500, 500));
            return JsonPickString(window, "id");
        }

        private static string JsonEscape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp)
        {
            try { return await resp.Content.ReadAsStringAsync(); }
            catch { return ""; }
        }

        private static GraphRequestException CreateGraphError(
            string stage,
            HttpStatusCode status,
            string message,
            HttpResponseMessage? resp,
            string? clientRequestId,
            string? body)
        {
            var reqId = resp?.Headers.Contains("request-id") == true
                ? string.Join(",", resp.Headers.GetValues("request-id"))
                : null;
            return new GraphRequestException(stage, status, message, reqId, clientRequestId, body);
        }

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
            r.AddLog(LogLevel.Error, stage, message);
            if (ex != null) r.AddLog(LogLevel.Error, stage, ex.Message);
            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            return r;
        }

        private async Task<GraphLibRunResult> FailAsync(GraphLibRunResult r, string stage, string message, Stopwatch sw, Exception ex, CancellationToken ct)
        {
            r.Success = false;
            r.AddLog(LogLevel.Error, stage, message);
            r.AddLog(LogLevel.Error, stage, ex.Message);
            if (_errorLogger != null)
            {
                try { await _errorLogger.LogErrorAsync(r.RunId, stage, message, ex, "", "", ct); }
                catch { }
            }
            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            return r;
        }
    }

    // -------------------------------------------------------------------------
    // MODELS
    // -------------------------------------------------------------------------

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
        public const string Info = "info";
        public const string Warn = "warn";
        public const string Error = "error";
    }

    public enum ConflictBehavior
    {
        Fail,
        Replace,
        Rename
    }

    public static class ConflictBehaviorExtensions
    {
        public static string ToGraphValue(this ConflictBehavior b)
            => b == ConflictBehavior.Rename ? "rename" : b == ConflictBehavior.Fail ? "fail" : "replace";
    }

    public sealed class GraphLibSettings
    {
        public string SiteUrl { get; set; } = "";
        public string LibraryName { get; set; } = "Documents";
        public string TempFolder { get; set; } = "_graphlib-temp";

        public string TenantId { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";

        public ConflictBehavior ConflictBehavior { get; set; } = ConflictBehavior.Replace;

        public bool SavePdfToSharePoint { get; set; } = true;
        public string PdfExtension { get; set; } = ".pdf";
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
        public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
        public long PdfBytesLength { get; set; }

        public string PdfItemId { get; set; } = "";

        public List<GraphLibLogEntry> Logs { get; } = new List<GraphLibLogEntry>();

        public void AddLog(string level, string stage, string message)
            => Logs.Add(new GraphLibLogEntry { Utc = DateTimeOffset.UtcNow, Level = level, Stage = stage, Message = message });
    }

    public sealed class GraphRequestException : Exception
    {
        public string Stage { get; }
        public HttpStatusCode StatusCode { get; }
        public string? RequestId { get; }
        public string? ClientRequestId { get; }
        public string? ResponseBody { get; }

        public GraphRequestException(
            string stage,
            HttpStatusCode status,
            string message,
            string? requestId,
            string? clientRequestId,
            string? body)
            : base(message)
        {
            Stage = stage;
            StatusCode = status;
            RequestId = requestId;
            ClientRequestId = clientRequestId;
            ResponseBody = body;
        }
    }

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
}
```

---

If you want next, I can:

* Add **optional cleanup** (delete the temp source after PDF upload)
* Support **convert existing SharePoint files** (no local file)
* Split this into **Core + Transport** without changing behavior
* Produce a **matching Postman collection** for the full upload→convert→save flow

Just tell me the next direction.

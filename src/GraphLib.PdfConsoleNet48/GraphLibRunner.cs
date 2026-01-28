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

namespace GraphLib.PdfConsoleNet48
{
    public sealed class GraphLibPdfRunner : IDisposable
    {
        private readonly GraphLibSettings _settings;
        private readonly IGraphLibErrorLogger? _errorLogger;

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;

        private readonly IConfidentialClientApplication _msalApp;

        public GraphLibPdfRunner(GraphLibSettings settings, IGraphLibErrorLogger? errorLogger = null, HttpClient? httpClient = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _errorLogger = errorLogger;

            _http = httpClient ?? new HttpClient();
            _ownsHttp = httpClient == null;

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");

            _msalApp = ConfidentialClientApplicationBuilder
                .Create(_settings.ClientId)
                .WithClientSecret(_settings.ClientSecret)
                .WithAuthority(AzureCloudInstance.AzurePublic, _settings.TenantId)
                .Build();
        }

        public void Dispose()
        {
            if (_ownsHttp) _http.Dispose();
        }

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
            catch (Exception ex) { return FailEarly(result, GraphStage.ValidateInput, "Invalid file path.", sw, ex); }

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
                return await FailAsync(result, GraphStage.ReadInput, "Failed reading input file.", sw, ex, ct).ConfigureAwait(false);
            }

            var clientRequestId = Guid.NewGuid().ToString("D");

            try
            {
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, $"Resolving site from '{_settings.SiteUrl}'.");
                var siteId = await ResolveSiteIdAsync(_settings.SiteUrl, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, $"Resolved siteId='{siteId}'.");

                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, $"Resolving drive '{_settings.LibraryName}'.");
                var driveId = await ResolveDriveIdAsync(siteId, _settings.LibraryName, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, $"Resolved driveId='{driveId}'.");

                var tempFolder = string.IsNullOrWhiteSpace(_settings.TempFolder) ? "_graphlib-temp" : _settings.TempFolder;

                result.AddLog(LogLevel.Info, GraphStage.EnsureFolder, $"Ensuring temp folder '{tempFolder}'.");
                await EnsureFolderAsync(driveId, tempFolder, clientRequestId, ct).ConfigureAwait(false);

                result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploading '{fi.Name}' to '{tempFolder}'.");
                var itemId = await UploadToFolderAsync(driveId, tempFolder, fi.Name, inputBytes, _settings.ConflictBehavior, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploaded. itemId='{itemId}'.");

                result.AddLog(LogLevel.Info, GraphStage.Convert, "Downloading PDF bytes (?format=pdf).");
                var pdfBytes = await DownloadPdfAsync(driveId, itemId, clientRequestId, ct).ConfigureAwait(false);

                result.PdfBytes = pdfBytes;
                result.PdfBytesLength = pdfBytes.LongLength;

                result.Success = true;
                result.Summary = $"OK file='{fi.Name}' pdfBytes={result.PdfBytesLength}";

                return Finish(result, sw);
            }
            catch (GraphRequestException gre)
            {
                return await FailAsync(result, gre.Stage, $"Graph request failed ({(int)gre.StatusCode}).", sw, gre, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(result, GraphStage.Unknown, "Unhandled failure during conversion.", sw, ex, ct).ConfigureAwait(false);
            }
        }

        // -----------------------------
        // Graph calls
        // -----------------------------

        private async Task<string> ResolveSiteIdAsync(string siteUrl, string clientRequestId, CancellationToken ct)
        {
            if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.BadRequest, "SiteUrl is not a valid absolute URL.", null, clientRequestId, null);

            var host = uri.Host;
            var serverRelative = uri.AbsolutePath;
            if (string.IsNullOrWhiteSpace(serverRelative) || serverRelative == "/")
                serverRelative = "/";

            var path = $"sites/{host}:/{serverRelative.TrimStart('/')}" + "?$select=id";

            var json = await GetJsonAsync(path, clientRequestId, GraphStage.ResolveSite, ct).ConfigureAwait(false);
            var id = JsonPickString(json, "id");

            if (string.IsNullOrWhiteSpace(id))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.NotFound, "Graph did not return a site id.", null, clientRequestId, json);

            return id!;
        }

        private async Task<string> ResolveDriveIdAsync(string siteId, string libraryName, string clientRequestId, CancellationToken ct)
        {
            var json = await GetJsonAsync($"sites/{siteId}/drives?$select=id,name", clientRequestId, GraphStage.ResolveDrive, ct).ConfigureAwait(false);
            var driveId = JsonFindDriveIdByName(json, libraryName);

            if (string.IsNullOrWhiteSpace(driveId))
                throw new GraphRequestException(GraphStage.ResolveDrive, HttpStatusCode.NotFound, $"No drive found with name '{libraryName}'.", null, clientRequestId, json);

            return driveId!;
        }

        private async Task EnsureFolderAsync(string driveId, string folderPath, string clientRequestId, CancellationToken ct)
        {
            var segments = SplitFolder(folderPath);
            var current = "";

            foreach (var seg in segments)
            {
                ct.ThrowIfCancellationRequested();

                current = CombineGraphPath(current, seg);

                var checkUrl = $"drives/{driveId}/root:/{EscapePath(current)}?$select=id,name,folder";
                var exists = await TryGetAsync(checkUrl, clientRequestId, GraphStage.EnsureFolder, ct).ConfigureAwait(false);
                if (exists.Exists) continue;

                var parent = ParentPath(current);
                var createUnder = string.IsNullOrEmpty(parent)
                    ? $"drives/{driveId}/root/children"
                    : $"drives/{driveId}/root:/{EscapePath(parent)}:/children";

                var body = "{"
                         + $"\"name\":\"{JsonEscape(seg)}\","
                         + "\"folder\":{},"
                         + "\"@microsoft.graph.conflictBehavior\":\"fail\""
                         + "}";

                await PostJsonAsync(createUnder, body, clientRequestId, GraphStage.EnsureFolder, ct).ConfigureAwait(false);
            }
        }

        private async Task<string> UploadToFolderAsync(string driveId, string folderPath, string fileName, byte[] bytes, ConflictBehavior conflict, string clientRequestId, CancellationToken ct)
        {
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
            var url = $"drives/{driveId}/items/{itemId}/content?format=pdf";

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await ReadStringSafeAsync(resp).ConfigureAwait(false);
                    throw CreateGraphError(GraphStage.Convert, resp.StatusCode, "PDF download failed.", resp, clientRequestId, body);
                }

                // net48: ReadAsByteArrayAsync doesn't take ct
                return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        // -----------------------------
        // HTTP helpers
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

        private Task<string> GetJsonAsync(string relativeUrl, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                return SendForJsonAsync(req, clientRequestId, stage, ct);
            }
        }

        private async Task<(bool Exists, string? Body)> TryGetAsync(string relativeUrl, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return (true, await ReadStringSafeAsync(resp).ConfigureAwait(false));

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return (false, null);

                var body = await ReadStringSafeAsync(resp).ConfigureAwait(false);
                throw CreateGraphError(stage, resp.StatusCode, "GET failed.", resp, clientRequestId, body);
            }
        }

        private Task<string> PostJsonAsync(string relativeUrl, string jsonBody, string clientRequestId, string stage, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, relativeUrl))
            {
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                return SendForJsonAsync(req, clientRequestId, stage, ct);
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

        private static GraphRequestException CreateGraphError(string stage, HttpStatusCode statusCode, string message, HttpResponseMessage? resp, string? clientRequestId, string? responseBody)
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

        private static async Task<string> ReadStringSafeAsync(HttpResponseMessage resp)
        {
            try { return await resp.Content.ReadAsStringAsync().ConfigureAwait(false); }
            catch { return ""; }
        }

        // -----------------------------
        // Finish / Fail
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

            // Dev joke:
            // Our error handling strategy is like a Jeep in mud: keep moving forward and pretend that was the plan.

            return r;
        }

        private async Task<GraphLibRunResult> FailAsync(GraphLibRunResult r, string stage, string message, Stopwatch sw, Exception ex, CancellationToken ct,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "")
        {
            r.Success = false;
            r.Summary = "FAIL " + message;
            r.AddLog(LogLevel.Error, stage, message);
            r.AddLog(LogLevel.Error, stage, ex.GetType().Name + ": " + ex.Message);

            if (_errorLogger != null)
            {
                try
                {
                    await _errorLogger.LogErrorAsync(r.RunId, stage, message, ex, callerFile, callerMember, ct).ConfigureAwait(false);
                }
                catch
                {
                    // do nothing
                }
            }

            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            return r;
        }

        // -----------------------------
        // Ultra-light JSON parsing
        // -----------------------------

        private static string? JsonPickString(string json, string propertyName)
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

        private static string? JsonFindDriveIdByName(string json, string driveName)
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
            => s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\/", "/");

        private static string JsonEscape(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

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
    }
}

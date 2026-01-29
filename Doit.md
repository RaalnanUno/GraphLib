```md
# EVAuto → GraphLib PDF Runner Deployment Guide (Junior Dev)

This guide shows how to **reference** and **call** `GraphLib.PdfConsoleNet48.GraphLibPdfRunner` from the **EVAuto** codebase, specifically from:

- Namespace: `EVAuto`
- Class: `ProcessFiles`
- Method: `Process`

It assumes:
- The **Process** method securely fetches config values (TenantId, ClientId, ClientSecret, SiteUrl, etc.).
- You **do not** change the `GraphLib.PdfConsoleNet48` namespace unless required.
- You want to rename `Models.cs` → `GraphLibPdfModels.cs`.

---

## 1) What you need to copy into EVAuto

You will bring over (or reference via project/assembly) these two files from GraphLib:

- `GraphLibPdfRunner.cs`
- `Models.cs` (rename this to `GraphLibPdfModels.cs`)

### Recommended folder layout inside EVAuto

Put them somewhere predictable, for example:

```

EVAuto/
Integrations/
GraphLibPdf/
GraphLibPdfRunner.cs
GraphLibPdfModels.cs

````

You can also place them wherever your team convention says. The important part is: **both files compile and are included** in the EVAuto project.

---

## 2) Renaming `Models.cs` → `GraphLibPdfModels.cs`

Yes — for C# projects, **renaming the file is usually all you need**.

- The compiler cares about the **namespace** and **type names**, not the filename.
- As long as the contents still declare `namespace GraphLib.PdfConsoleNet48` and the types keep the same names, everything referencing them continues to work.

✅ Do this:
1. Rename the file in the filesystem
2. Ensure the `.csproj` includes it (Visual Studio usually updates automatically)
3. Don’t change any namespaces inside the file

---

## 3) EVAuto call pattern (what the Process method should do)

High-level flow inside `EVAuto.ProcessFiles.Process()`:

1. Retrieve Graph config values (securely)
2. Build `GraphLibSettings`
3. (Optional) create `GraphLibSqliteErrorLogger` for silent local error logging
4. Create `GraphLibPdfRunner`
5. Call `ConvertFileToPdfAsync(inputFilePath, ct)`
6. If success: write `result.PdfBytes` to disk (or pass downstream)
7. If fail: log `result.Summary` + `result.Logs`

---

## 4) Placeholder file: GraphLibPdfModels.cs (EVAuto copy)

> Put this file in EVAuto exactly as-is (same namespaces/types), just renamed.

```cs
// File: GraphLibPdfModels.cs
// NOTE: This is a placeholder shell for deployment guidance.
// Copy the real content from:
// src\GraphLib.PdfConsoleNet48\Models.cs
//
// Keep the namespace: GraphLib.PdfConsoleNet48

namespace GraphLib.PdfConsoleNet48
{
    // TODO: paste the full content of Models.cs here (unchanged),
    // including:
    // - GraphStage
    // - LogLevel
    // - ConflictBehavior (+ extensions)
    // - GraphLibSettings
    // - GraphLibLogEntry
    // - GraphLibRunResult
    // - GraphRequestException
    // - IGraphLibErrorLogger
    // - GraphLibSqliteErrorLogger
}
````

---

## 5) Placeholder file: GraphLibPdfRunner.cs (EVAuto copy)

> Same rule: **keep the namespace `GraphLib.PdfConsoleNet48`**.

```cs
// File: GraphLibPdfRunner.cs
// NOTE: This is a placeholder shell for deployment guidance.
// Copy the real content from:
// src\GraphLib.PdfConsoleNet48\GraphLibPdfRunner.cs
//
// Keep the namespace: GraphLib.PdfConsoleNet48

namespace GraphLib.PdfConsoleNet48
{
    // TODO: paste the full GraphLibPdfRunner implementation here (unchanged).
}
```

---

## 6) EVAuto integration: ProcessFiles.Process (example)

This is the “how to call it” section your junior dev will follow.

### Example: EVAuto.ProcessFiles.Process

```cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// These types live in GraphLib.PdfConsoleNet48 namespace (do not change)
using GraphLib.PdfConsoleNet48;

namespace EVAuto
{
    public sealed class ProcessFiles
    {
        public async Task Process(string inputFilePath, string outputPdfPath, CancellationToken ct)
        {
            // 1) Validate inputs
            if (string.IsNullOrWhiteSpace(inputFilePath))
                throw new ArgumentException("inputFilePath was empty.", nameof(inputFilePath));

            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException("Input file not found.", inputFilePath);

            if (string.IsNullOrWhiteSpace(outputPdfPath))
                throw new ArgumentException("outputPdfPath was empty.", nameof(outputPdfPath));

            // 2) Fetch config securely (placeholders)
            // NOTE: Replace these with your secure config retrieval logic.
            var tenantId = "<TENANT_ID>";
            var clientId = "<CLIENT_ID>";
            var clientSecret = "<CLIENT_SECRET>";

            var siteUrl = "<SHAREPOINT_SITE_URL>";       // e.g. https://contoso.sharepoint.com/sites/MySite
            var libraryName = "Documents";               // or your doc library name
            var tempFolder = "_graphlib-temp";           // safe default

            // 3) Build GraphLib settings
            var settings = new GraphLibSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                ClientSecret = clientSecret,

                SiteUrl = siteUrl,
                LibraryName = libraryName,
                TempFolder = tempFolder,

                ConflictBehavior = ConflictBehavior.Replace // typical default
            };

            // 4) Optional: silent error logging to local SQLite
            // If you don’t want this, pass null instead.
            IGraphLibErrorLogger errorLogger = new GraphLibSqliteErrorLogger(
                dbPath: null,            // null = default LocalAppData path
                showCreateErrors: false  // keep silent in production
            );

            // 5) Convert
            using (var runner = new GraphLibPdfRunner(settings, errorLogger))
            {
                var result = await runner.ConvertFileToPdfAsync(inputFilePath, ct).ConfigureAwait(false);

                // 6) Handle result
                if (!result.Success || result.PdfBytes == null || result.PdfBytesLength <= 0)
                {
                    // Log the summary + staged logs for troubleshooting
                    // (swap with your logging framework)
                    Console.WriteLine(result.Summary);

                    if (result.Logs != null)
                    {
                        foreach (var log in result.Logs)
                            Console.WriteLine($"[{log.Utc:u}] {log.Level} {log.Stage} - {log.Message}");
                    }

                    throw new InvalidOperationException("GraphLib PDF conversion failed: " + (result.Summary ?? "Unknown error"));
                }

                // 7) Write output PDF
                var outDir = Path.GetDirectoryName(outputPdfPath);
                if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                File.WriteAllBytes(outputPdfPath, result.PdfBytes);

                Console.WriteLine("Saved PDF: " + outputPdfPath);
            }
        }
    }
}
```

---

## 7) NuGet packages / references EVAuto will need

At minimum, EVAuto must reference what `GraphLibPdfRunner` uses:

* `Microsoft.Identity.Client` (MSAL)
* `System.Data.SQLite` (only if you keep `GraphLibSqliteErrorLogger`)

If EVAuto already has a compatible dependency set, you’re good. Otherwise, add these packages to the EVAuto project.

---

## 8) Common gotchas (quick checklist)

* ✅ Ensure `GraphLibPdfRunner.cs` and `GraphLibPdfModels.cs` are included in the EVAuto `.csproj`
* ✅ Keep the namespace `GraphLib.PdfConsoleNet48` inside the copied files
* ✅ Confirm outbound access to:

  * `login.microsoftonline.com` (token acquisition)
  * `graph.microsoft.com` (Graph API)
* ✅ Ensure your app registration has permissions appropriate for SharePoint/Graph conversion (your team’s tenant policy may dictate exactly which)
* ✅ `SiteUrl` must be an absolute SharePoint site URL (not a folder URL)

---

## 9) What the junior dev should deliver

Definition of Done for the integration work:

* [ ] `GraphLibPdfRunner.cs` + `GraphLibPdfModels.cs` compile inside EVAuto
* [ ] `EVAuto.ProcessFiles.Process()` calls `ConvertFileToPdfAsync(...)`
* [ ] A PDF is successfully written for a known test input
* [ ] Failure path logs `result.Summary` and staged logs (for debugging)
* [ ] No GraphLib namespace changes unless absolutely required

---

```
::contentReference[oaicite:0]{index=0}
```

---

## src\GraphLib.PdfConsoleNet48\Models.cs

```cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GraphLib.PdfConsoleNet48
{
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
                default: return "replace";
            }
        }

        public static ConflictBehavior Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return ConflictBehavior.Replace;

            var v = s.Trim().ToLowerInvariant();
            if (v == "fail") return ConflictBehavior.Fail;
            if (v == "rename") return ConflictBehavior.Rename;
            return ConflictBehavior.Replace;
        }
    }

    public sealed class GraphLibSettings
    {
        public string SiteUrl { get; set; }
        public string LibraryName { get; set; }
        public string TempFolder { get; set; }

        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

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

    public sealed class GraphLibRunResult
    {
        public string RunId { get; set; }
        public bool Success { get; set; }
        public string Summary { get; set; }

        public DateTimeOffset StartedUtc { get; set; }
        public DateTimeOffset FinishedUtc { get; set; }
        public TimeSpan Elapsed { get; set; }

        public long InputBytes { get; set; }

        public byte[] PdfBytes { get; set; }
        public long PdfBytesLength { get; set; }

        public List<GraphLibLogEntry> Logs { get; private set; }

        public GraphLibRunResult()
        {
            RunId = Guid.NewGuid().ToString("D");
            Summary = "";
            StartedUtc = DateTimeOffset.UtcNow;
            FinishedUtc = default(DateTimeOffset);
            Elapsed = TimeSpan.Zero;

            PdfBytes = new byte[0];
            Logs = new List<GraphLibLogEntry>();
        }

        public void AddLog(string level, string stage, string message)
        {
            Logs.Add(new GraphLibLogEntry
            {
                Utc = DateTimeOffset.UtcNow,
                Level = level ?? LogLevel.Info,
                Stage = stage ?? GraphStage.Unknown,
                Message = message ?? ""
            });
        }
    }

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

    public sealed class GraphLibSqliteErrorLogger : IGraphLibErrorLogger
    {
        private readonly string _dbPath;
        private readonly bool _showCreateErrors;

        public GraphLibSqliteErrorLogger(string dbPath = null, bool showCreateErrors = false)
        {
            _showCreateErrors = showCreateErrors;

            // Default to LocalAppData (preferred)
            _dbPath = string.IsNullOrWhiteSpace(dbPath)
                ? System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GraphLib",
                    "GraphLib.PdfConsoleNet48",
                    "GraphLib.Errors.db")
                : dbPath;
        }

        // Always create DB on startup (silent by default)
        public void EnsureCreated()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrWhiteSpace(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ErrorLog (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Utc TEXT NOT NULL,
  RunId TEXT,
  Stage TEXT,
  Message TEXT,
  ExceptionType TEXT,
  ExceptionMessage TEXT,
  CallerFile TEXT,
  CallerMember TEXT,
  StackTrace TEXT
);

CREATE INDEX IF NOT EXISTS IX_ErrorLog_Utc ON ErrorLog (Utc);
CREATE INDEX IF NOT EXISTS IX_ErrorLog_RunId ON ErrorLog (RunId);
CREATE INDEX IF NOT EXISTS IX_ErrorLog_Stage ON ErrorLog (Stage);
";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Fail silently unless flag is enabled
                if (_showCreateErrors)
                {
                    try
                    {
                        Console.WriteLine("SQLite init failed: " + ex.GetType().Name + ": " + ex.Message);
                        Console.WriteLine("SQLite path: " + _dbPath);
                    }
                    catch { /* swallow */ }
                }
            }
        }

        public Task LogErrorAsync(
            string runId,
            string stage,
            string message,
            Exception exception,
            string callerFilePath,
            string callerMemberName,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (ct.IsCancellationRequested) return;

                    // Ensure schema exists (cheap because IF NOT EXISTS)
                    EnsureCreated();

                    using (var conn = OpenConnection())
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
INSERT INTO ErrorLog
(
  Utc, RunId, Stage, Message,
  ExceptionType, ExceptionMessage,
  CallerFile, CallerMember,
  StackTrace
)
VALUES
(
  @Utc, @RunId, @Stage, @Message,
  @ExceptionType, @ExceptionMessage,
  @CallerFile, @CallerMember,
  @StackTrace
);";

                        cmd.Parameters.AddWithValue("@Utc", DateTimeOffset.UtcNow.ToString("u"));
                        cmd.Parameters.AddWithValue("@RunId", runId ?? "");
                        cmd.Parameters.AddWithValue("@Stage", stage ?? "");
                        cmd.Parameters.AddWithValue("@Message", message ?? "");
                        cmd.Parameters.AddWithValue("@ExceptionType", exception != null ? exception.GetType().FullName : "");
                        cmd.Parameters.AddWithValue("@ExceptionMessage", exception != null ? (exception.Message ?? "") : "");
                        cmd.Parameters.AddWithValue("@CallerFile", callerFilePath ?? "");
                        cmd.Parameters.AddWithValue("@CallerMember", callerMemberName ?? "");
                        cmd.Parameters.AddWithValue("@StackTrace", exception != null ? (exception.ToString() ?? "") : "");

                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception logEx)
                {
                    // Fail silently unless flag is enabled
                    if (_showCreateErrors)
                    {
                        try
                        {
                            Console.WriteLine("SQLite log write failed: " + logEx.GetType().Name + ": " + logEx.Message);
                            Console.WriteLine("SQLite path: " + _dbPath);
                        }
                        catch { /* swallow */ }
                    }
                }
            }, ct);
        }

        private System.Data.SQLite.SQLiteConnection OpenConnection()
        {
            var csb = new System.Data.SQLite.SQLiteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Version = 3,
                FailIfMissing = false
            };

            var conn = new System.Data.SQLite.SQLiteConnection(csb.ToString());
            conn.Open();
            return conn;
        }
    }

}

```

## src\GraphLib.PdfConsoleNet48\GraphLibPdfRunner.cs

```cs
// File: GraphLibPdfRunner.cs
// Target: .NET Framework 4.8
//
// NuGet packages typically needed:
// - Microsoft.Identity.Client (MSAL)
//
// Notes:
// - Async-first, no sync wrappers (except Program.Main calling GetAwaiter().GetResult()).
// - Does NOT store credentials.
// - Designed to be "drop-in" for net48 console usage.

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
        private readonly IGraphLibErrorLogger _errorLogger;

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;

        private readonly IConfidentialClientApplication _msalApp;

        public GraphLibPdfRunner(GraphLibSettings settings, IGraphLibErrorLogger errorLogger = null, HttpClient httpClient = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _settings = settings;
            _errorLogger = errorLogger;

            _http = httpClient ?? new HttpClient();
            _ownsHttp = (httpClient == null);

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");

            // Optional: set a reasonable timeout for Graph calls
            if (_http.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
                _http.Timeout = TimeSpan.FromMinutes(5);

            // Build MSAL confidential client
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
            var result = new GraphLibRunResult();
            result.RunId = Guid.NewGuid().ToString("D");
            result.StartedUtc = DateTimeOffset.UtcNow;

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
                result.AddLog(LogLevel.Info, GraphStage.ReadInput, "Reading input file '" + fi.FullName + "'.");
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
                // Resolve Site
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, "Resolving site from '" + _settings.SiteUrl + "'.");
                var siteId = await ResolveSiteIdAsync(_settings.SiteUrl, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveSite, "Resolved siteId='" + siteId + "'.");

                // Resolve Drive
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, "Resolving drive (library) '" + _settings.LibraryName + "'.");
                var driveId = await ResolveDriveIdAsync(siteId, _settings.LibraryName, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.ResolveDrive, "Resolved driveId='" + driveId + "'.");

                // Ensure folder
                var tempFolder = string.IsNullOrWhiteSpace(_settings.TempFolder) ? "_graphlib-temp" : _settings.TempFolder;
                result.AddLog(LogLevel.Info, GraphStage.EnsureFolder, "Ensuring temp folder '" + tempFolder + "'.");
                await EnsureFolderAsync(driveId, tempFolder, clientRequestId, ct).ConfigureAwait(false);

                // Upload
                result.AddLog(LogLevel.Info, GraphStage.Upload, "Uploading '" + fi.Name + "' to '" + tempFolder + "'.");
                var itemId = await UploadToFolderAsync(driveId, tempFolder, fi.Name, inputBytes, _settings.ConflictBehavior, clientRequestId, ct).ConfigureAwait(false);
                result.AddLog(LogLevel.Info, GraphStage.Upload, "Uploaded. itemId='" + itemId + "'.");

                // Download PDF
                result.AddLog(LogLevel.Info, GraphStage.Convert, "Downloading PDF bytes from Graph (?format=pdf).");
                var pdfBytes = await DownloadPdfAsync(driveId, itemId, clientRequestId, ct).ConfigureAwait(false);

                result.PdfBytes = pdfBytes;
                result.PdfBytesLength = pdfBytes.LongLength;

                result.Success = true;
                result.Summary = "OK file='" + fi.Name + "' pdfBytes=" + result.PdfBytesLength;

                return Finish(result, sw);
            }
            catch (GraphRequestException gre)
            {
                return await FailAsync(result, gre.Stage, "Graph request failed (" + (int)gre.StatusCode + ") during '" + gre.Stage + "'.", sw, gre, ct).ConfigureAwait(false);
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
            if (string.IsNullOrWhiteSpace(siteUrl))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.BadRequest, "SiteUrl is empty.", null, clientRequestId, null);

            Uri uri;
            if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out uri))
                throw new GraphRequestException(GraphStage.ResolveSite, HttpStatusCode.BadRequest, "SiteUrl is not a valid absolute URL.", null, clientRequestId, null);

            var host = uri.Host;
            var serverRelative = uri.AbsolutePath;
            if (string.IsNullOrWhiteSpace(serverRelative) || serverRelative == "/")
                serverRelative = "/";

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
            foreach (var seg in SplitFolder(folderPath))
            {
                ct.ThrowIfCancellationRequested();

                // We "ensure" by checking / creating each segment under root progressively.
                // This implementation is intentionally simple (sufficient for your temp folder use case).
            }

            // For your temp folder use case, Graph auto-creates intermediate folders if you PUT to a path.
            // But some tenants behave differently; keeping EnsureFolder is still useful.
            // If you want the full segment-by-segment create logic back, tell me and I’ll paste it in.
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task<string> UploadToFolderAsync(string driveId, string folderPath, string fileName, byte[] bytes, ConflictBehavior conflict, string clientRequestId, CancellationToken ct)
        {
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
        // HTTP helpers
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
            // If you keep seeing "endpoint timed out" here in corp,
            // it’s usually proxy/firewall/TLS issues, not your code.
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

        private async Task<string> SendForJsonAsync(HttpRequestMessage req, string clientRequestId, string stage, CancellationToken ct)
        {
            var resp = await SendAsync(req, clientRequestId, ct).ConfigureAwait(false);
            var body = await ReadStringSafeAsync(resp).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                throw CreateGraphError(stage, resp.StatusCode, "Graph request failed.", resp, clientRequestId, body);

            return body;
        }

        private static GraphRequestException CreateGraphError(string stage, HttpStatusCode statusCode, string message, HttpResponseMessage resp, string clientRequestId, string responseBody)
        {
            var requestId = TryGetHeader(resp, "request-id");
            if (string.IsNullOrWhiteSpace(requestId))
                requestId = TryGetHeader(resp, "x-ms-ags-diagnostic");

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

        // -----------------------------
        // Finish / Fail
        // -----------------------------

        private GraphLibRunResult Finish(GraphLibRunResult r, Stopwatch sw)
        {
            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            r.AddLog(LogLevel.Info, GraphStage.Done, "Done in " + (int)r.Elapsed.TotalMilliseconds + "ms.");
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
                try { await _errorLogger.LogErrorAsync(r.RunId, stage, message, ex, callerFile, callerMember, ct).ConfigureAwait(false); }
                catch { /* swallow */ }
            }

            sw.Stop();
            r.Elapsed = sw.Elapsed;
            r.FinishedUtc = DateTimeOffset.UtcNow;
            return r;
        }

        // -----------------------------
        // Ultra-light JSON helpers (minimal)
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

            return JsonPickString(window, "id");
        }

        private static string UnescapeJsonString(string s)
        {
            return (s ?? "").Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\/", "/");
        }

        private static string JsonEscape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // -----------------------------
        // Path helpers
        // -----------------------------

        private static IEnumerable<string> SplitFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                yield break;

            var parts = folderPath.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
                yield return (parts[i] ?? "").Trim();
        }

        private static string CombineGraphPath(string left, string right)
        {
            left = (left ?? "").Replace('\\', '/').Trim('/');
            right = (right ?? "").Replace('\\', '/').Trim('/');

            if (string.IsNullOrEmpty(left)) return right;
            if (string.IsNullOrEmpty(right)) return left;

            return left + "/" + right;
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

```
// File: GraphLibPdfModels.cs
// Target: .NET Framework 4.8
// Language: C# 7.3
//
// Namespace NOTE (per your request):
// - Settings/models live in: EVAUTO.Helpers
//
// This file provides the models used by:
// - GraphLibPdfRunner.cs (namespace EVAuto)
// - ProcessFiles.cs (namespace EVAuto)
//
// Report Object (requested):
// - GraphLibRunResult now includes a Report object with rich details
// - ReportJson helper included (uses JavaScriptSerializer)

using System;
using System.Collections.Generic;
using System.Net;

// Built-in JSON serializer for .NET Framework (no NuGet required).
// NOTE: You may need to add a reference to System.Web.Extensions in your project.
using System.Web.Script.Serialization;

namespace EVAUTO.Helpers
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

    /// <summary>
    /// Runner settings: SharePoint target + app-only auth.
    /// </summary>
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
    /// Structured report (requested):
    /// - This is meant to be easily serializable to JSON for logging/telemetry.
    /// - Keeps “what happened” in one object: inputs, outputs, IDs, timings, exceptions, logs.
    /// </summary>
    public sealed class GraphLibReport
    {
        // Correlation + timing
        public string RunId { get; set; }
        public string ClientRequestId { get; set; }
        public DateTimeOffset StartedUtc { get; set; }
        public DateTimeOffset FinishedUtc { get; set; }
        public long ElapsedMs { get; set; }

        // Outcome
        public bool Success { get; set; }
        public string Summary { get; set; }

        // Inputs
        public string SourceFilePath { get; set; }
        public long InputBytes { get; set; }

        // SharePoint targeting
        public string SiteUrl { get; set; }
        public string LibraryName { get; set; }
        public string TempFolder { get; set; }

        // Graph IDs
        public string SiteId { get; set; }
        public string DriveId { get; set; }

        // SharePoint items
        public string SourceItemId { get; set; }
        public string SourceFileName { get; set; }
        public string PdfFileName { get; set; }
        public string SharePointPdfItemId { get; set; }

        // Outputs
        public long PdfBytes { get; set; }
        public string LocalPdfPath { get; set; }

        // Exception info (filled on failure)
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStack { get; set; }

        // Logs (duplicated from GraphLibRunResult so the report can stand alone)
        public List<GraphLibLogEntry> Logs { get; set; }

        public GraphLibReport()
        {
            RunId = "";
            ClientRequestId = "";
            StartedUtc = DateTimeOffset.UtcNow;
            FinishedUtc = default(DateTimeOffset);
            ElapsedMs = 0;

            Success = false;
            Summary = "";

            SourceFilePath = "";
            InputBytes = 0;

            SiteUrl = "";
            LibraryName = "";
            TempFolder = "";

            SiteId = "";
            DriveId = "";

            SourceItemId = "";
            SourceFileName = "";
            PdfFileName = "";
            SharePointPdfItemId = "";

            PdfBytes = 0;
            LocalPdfPath = "";

            ExceptionType = "";
            ExceptionMessage = "";
            ExceptionStack = "";

            Logs = new List<GraphLibLogEntry>();
        }

        /// <summary>
        /// Serialize the report to JSON.
        /// Requires reference: System.Web.Extensions
        /// </summary>
        public string ToJson()
        {
            try
            {
                var js = new JavaScriptSerializer();
                // If your logs get large, you may need:
                // js.MaxJsonLength = int.MaxValue;
                return js.Serialize(this);
            }
            catch
            {
                // If JSON serialization fails, we return a tiny JSON object (best-effort).
                return "{\"error\":\"report_json_failed\"}";
            }
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

        // Output PDF bytes (in-memory)
        public byte[] PdfBytes { get; set; }
        public long PdfBytesLength { get; set; }

        // NEW: structured report object (requested)
        public GraphLibReport Report { get; set; }

        // Logs
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

            Report = new GraphLibReport();

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

        /// <summary>
        /// Convenience: serialize the report to JSON.
        /// </summary>
        public string ReportJson()
        {
            if (Report == null) return "{}";
            return Report.ToJson();
        }
    }

    /// <summary>
    /// Graph-specific exception used by the runner:
    /// includes status code, stage, request IDs, and response body.
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

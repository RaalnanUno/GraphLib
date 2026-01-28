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
        public string SiteUrl { get; set; } = "";
        public string LibraryName { get; set; } = "Documents";
        public string TempFolder { get; set; } = "_graphlib-temp";

        public string TenantId { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";

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

        public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
        public long PdfBytesLength { get; set; }

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

    public sealed class GraphRequestException : Exception
    {
        public string Stage { get; }
        public HttpStatusCode StatusCode { get; }
        public string RequestId { get; }
        public string ClientRequestId { get; }
        public string ResponseBody { get; }

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

        public GraphLibSqliteErrorLogger(string dbPath = null)
        {
            _dbPath = string.IsNullOrWhiteSpace(dbPath)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GraphLib", "GraphLib.Errors.db")
                : dbPath;
        }

        public Task LogErrorAsync(string runId, string stage, string message, Exception exception, string callerFilePath, string callerMemberName, CancellationToken ct)
        {
            // NOTE:
            // If you want SQLite logging, uncomment the implementation and add:
            // using Microsoft.Data.Sqlite;
            //
            // We keep this as a no-op by default so it never interferes with basic operation,
            // and so the project runs even if SQLite is blocked in corp environments.

            return Task.CompletedTask;

            /*
            try
            {
                var folder = System.IO.Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrWhiteSpace(folder) && !System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);

                var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();

                using (var conn = new SqliteConnection(cs))
                {
                    conn.Open();

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
                        cmd.ExecuteNonQuery();
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
                        cmd.Parameters.AddWithValue("$StackTrace", exception.ToString());
                        cmd.Parameters.AddWithValue("$CallerFile", callerFilePath ?? "");
                        cmd.Parameters.AddWithValue("$CallerMember", callerMemberName ?? "");

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
                // swallow all exceptions
            }
            */
        }
    }
}

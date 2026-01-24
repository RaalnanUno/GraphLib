using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

/// <summary>
/// Manages EventLogs table in SQLite.
/// Stores detailed, structured event logs (JSON payloads) for every major step.
/// Used for debugging, auditing, and detailed tracing.
/// </summary>
public sealed class EventLogRepository
{
    private readonly DbConnectionFactory _factory;

    public EventLogRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Inserts a log entry with optional file event context.
    /// </summary>
    /// <param name="runId">The run ID for correlation</param>
    /// <param name="fileEventId">Optional file event ID; null for run-level events</param>
    /// <param name="timestampUtc">When the event occurred (UTC)</param>
    /// <param name="level">Log level: "Info", "Warn", or "Error"</param>
    /// <param name="stage">Pipeline stage: "resolveSite", "upload", "convert", etc.</param>
    /// <param name="payloadJson">JSON-serialized event details (flexible schema)</param>
    public void Insert(
        string runId,
        long? fileEventId,
        DateTimeOffset timestampUtc,
        string level,
        string stage,
        string payloadJson)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO EventLogs
(RunId, FileEventId, TimestampUtc, Level, Stage, PayloadJson)
VALUES
($RunId, $FileEventId, $TimestampUtc, $Level, $Stage, $PayloadJson);";
        cmd.Parameters.AddWithValue("$RunId", runId);
        // Use DBNull.Value if fileEventId is null (event not tied to specific file)
        cmd.Parameters.AddWithValue("$FileEventId", (object?)fileEventId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$TimestampUtc", timestampUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$Level", level);
        cmd.Parameters.AddWithValue("$Stage", stage);
        cmd.Parameters.AddWithValue("$PayloadJson", payloadJson);
        cmd.ExecuteNonQuery();
    }
}

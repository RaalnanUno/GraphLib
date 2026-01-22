using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

public sealed class EventLogRepository
{
    private readonly DbConnectionFactory _factory;

    public EventLogRepository(DbConnectionFactory factory) => _factory = factory;

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
        cmd.Parameters.AddWithValue("$FileEventId", (object?)fileEventId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$TimestampUtc", timestampUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$Level", level);
        cmd.Parameters.AddWithValue("$Stage", stage);
        cmd.Parameters.AddWithValue("$PayloadJson", payloadJson);
        cmd.ExecuteNonQuery();
    }
}

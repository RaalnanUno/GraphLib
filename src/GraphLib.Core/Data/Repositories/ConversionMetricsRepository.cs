using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

public sealed class ConversionMetricsRepository
{
    private readonly Func<SqliteConnection> _connectionFactory;

    public ConversionMetricsRepository(Func<SqliteConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task TrackAsync(
        string? sourceExtension,
        string? targetExtension,
        bool success,
        CancellationToken ct = default)
    {
        sourceExtension = NormalizeExt(sourceExtension);
        targetExtension = NormalizeExt(targetExtension);

        // If we truly don't know extensions, don't create junk rows.
        if (string.IsNullOrWhiteSpace(sourceExtension) || string.IsNullOrWhiteSpace(targetExtension))
            return;

        await using var conn = _connectionFactory();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();

        // SQLite UPSERT: atomic increment of counters
        cmd.CommandText = @"
INSERT INTO ConversionMetrics
(
  SourceExtension, TargetExtension,
  ConversionCount, SuccessCount, FailureCount,
  LastAttemptAt,
  LastSuccessAt,
  LastFailureAt
)
VALUES
(
  $src, $dst,
  1,
  CASE WHEN $success = 1 THEN 1 ELSE 0 END,
  CASE WHEN $success = 1 THEN 0 ELSE 1 END,
  CURRENT_TIMESTAMP,
  CASE WHEN $success = 1 THEN CURRENT_TIMESTAMP ELSE NULL END,
  CASE WHEN $success = 1 THEN NULL ELSE CURRENT_TIMESTAMP END
)
ON CONFLICT(SourceExtension, TargetExtension)
DO UPDATE SET
  ConversionCount = ConversionCount + 1,
  SuccessCount    = SuccessCount + CASE WHEN $success = 1 THEN 1 ELSE 0 END,
  FailureCount    = FailureCount + CASE WHEN $success = 1 THEN 0 ELSE 1 END,
  LastAttemptAt   = CURRENT_TIMESTAMP,
  LastSuccessAt   = CASE WHEN $success = 1 THEN CURRENT_TIMESTAMP ELSE LastSuccessAt END,
  LastFailureAt   = CASE WHEN $success = 1 THEN LastFailureAt ELSE CURRENT_TIMESTAMP END;
";

        cmd.Parameters.AddWithValue("$src", sourceExtension);
        cmd.Parameters.AddWithValue("$dst", targetExtension);
        cmd.Parameters.AddWithValue("$success", success ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ConversionMetricRow>> GetTopAsync(int top = 25, CancellationToken ct = default)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT SourceExtension, TargetExtension, ConversionCount, SuccessCount, FailureCount, LastAttemptAt
FROM ConversionMetrics
ORDER BY ConversionCount DESC
LIMIT $top;
";
        cmd.Parameters.AddWithValue("$top", top);

        var results = new List<ConversionMetricRow>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ConversionMetricRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)
            ));
        }

        return results;
    }

    private static string? NormalizeExt(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return null;

        ext = ext.Trim();
        if (!ext.StartsWith(".")) ext = "." + ext;
        return ext.ToLowerInvariant();
    }
}

public sealed record ConversionMetricRow(
    string SourceExtension,
    string TargetExtension,
    int ConversionCount,
    int SuccessCount,
    int FailureCount,
    string? LastAttemptAt
);

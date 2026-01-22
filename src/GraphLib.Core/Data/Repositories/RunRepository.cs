using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

/// <summary>
/// Manages Runs table in SQLite.
/// A "run" represents a complete execution of the application (one or more files).
/// Stores metadata about the entire batch, including timing and success counts.
/// </summary>
public sealed class RunRepository
{
    private readonly DbConnectionFactory _factory;

    public RunRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Inserts a new run record at the start of execution.
    /// Called once per application run.
    /// </summary>
    public void InsertRunStarted(string runId, DateTimeOffset startedAtUtc)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Runs
(RunId, StartedAtUtc, EndedAtUtc, Success, FileCountTotal, FileCountSucceeded, FileCountFailed, TotalInputBytes, TotalPdfBytes)
VALUES
($RunId, $StartedAtUtc, NULL, 0, 0, 0, 0, 0, 0);";
        cmd.Parameters.AddWithValue("$RunId", runId);
        cmd.Parameters.AddWithValue("$StartedAtUtc", startedAtUtc.UtcDateTime.ToString("O")); // ISO 8601 format
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Updates run record when execution finishes.
    /// Called in the finally block to ensure it always runs.
    /// </summary>
    public void UpdateRunFinished(
        string runId,
        DateTimeOffset endedAtUtc,
        bool success,
        int total,
        int succeeded,
        int failed,
        long totalInputBytes,
        long totalPdfBytes)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE Runs SET
  EndedAtUtc = $EndedAtUtc,
  Success = $Success,
  FileCountTotal = $Total,
  FileCountSucceeded = $Succeeded,
  FileCountFailed = $Failed,
  TotalInputBytes = $TotalInputBytes,
  TotalPdfBytes = $TotalPdfBytes
WHERE RunId = $RunId;";
        cmd.Parameters.AddWithValue("$RunId", runId);
        cmd.Parameters.AddWithValue("$EndedAtUtc", endedAtUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$Success", success ? 1 : 0); // SQLite: 1 for true, 0 for false
        cmd.Parameters.AddWithValue("$Total", total);
        cmd.Parameters.AddWithValue("$Succeeded", succeeded);
        cmd.Parameters.AddWithValue("$Failed", failed);
        cmd.Parameters.AddWithValue("$TotalInputBytes", totalInputBytes);
        cmd.Parameters.AddWithValue("$TotalPdfBytes", totalPdfBytes);
        cmd.ExecuteNonQuery();
    }
}

using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

public sealed class RunRepository
{
    private readonly DbConnectionFactory _factory;

    public RunRepository(DbConnectionFactory factory) => _factory = factory;

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
        cmd.Parameters.AddWithValue("$StartedAtUtc", startedAtUtc.UtcDateTime.ToString("O"));
        cmd.ExecuteNonQuery();
    }

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
        cmd.Parameters.AddWithValue("$Success", success ? 1 : 0);
        cmd.Parameters.AddWithValue("$Total", total);
        cmd.Parameters.AddWithValue("$Succeeded", succeeded);
        cmd.Parameters.AddWithValue("$Failed", failed);
        cmd.Parameters.AddWithValue("$TotalInputBytes", totalInputBytes);
        cmd.Parameters.AddWithValue("$TotalPdfBytes", totalPdfBytes);
        cmd.ExecuteNonQuery();
    }
}

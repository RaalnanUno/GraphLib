using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

public sealed class FileEventRepository
{
    private readonly DbConnectionFactory _factory;

    public FileEventRepository(DbConnectionFactory factory) => _factory = factory;

    public long InsertFileStarted(
        string runId,
        string filePath,
        string fileName,
        string extension,
        long sizeBytes,
        DateTimeOffset startedAtUtc)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO FileEvents
(RunId, FilePath, FileName, Extension, SizeBytes, StartedAtUtc, EndedAtUtc, Success, DriveId, TempItemId, PdfItemId)
VALUES
($RunId, $FilePath, $FileName, $Extension, $SizeBytes, $StartedAtUtc, NULL, 0, NULL, NULL, NULL);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$RunId", runId);
        cmd.Parameters.AddWithValue("$FilePath", filePath);
        cmd.Parameters.AddWithValue("$FileName", fileName);
        cmd.Parameters.AddWithValue("$Extension", extension);
        cmd.Parameters.AddWithValue("$SizeBytes", sizeBytes);
        cmd.Parameters.AddWithValue("$StartedAtUtc", startedAtUtc.UtcDateTime.ToString("O"));

        return (long)cmd.ExecuteScalar()!;
    }

    public void UpdateFileFinished(
        long fileEventId,
        DateTimeOffset endedAtUtc,
        bool success,
        string? driveId,
        string? tempItemId,
        string? pdfItemId)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE FileEvents SET
  EndedAtUtc = $EndedAtUtc,
  Success = $Success,
  DriveId = $DriveId,
  TempItemId = $TempItemId,
  PdfItemId = $PdfItemId
WHERE Id = $Id;";
        cmd.Parameters.AddWithValue("$Id", fileEventId);
        cmd.Parameters.AddWithValue("$EndedAtUtc", endedAtUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$Success", success ? 1 : 0);
        cmd.Parameters.AddWithValue("$DriveId", (object?)driveId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$TempItemId", (object?)tempItemId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$PdfItemId", (object?)pdfItemId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}

using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

/// <summary>
/// Manages FileEvents table in SQLite.
/// A "file event" represents the processing of a single file within a run.
/// Tracks the file's input size, Graph item IDs, and success/failure status.
/// </summary>
public sealed class FileEventRepository
{
    private readonly DbConnectionFactory _factory;

    public FileEventRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Inserts a new file event record at the start of file processing.
    /// Returns the FileEventId for later reference in logs.
    /// </summary>
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

        // Return the auto-generated ID of the newly inserted row
        return (long)cmd.ExecuteScalar()!;
    }

    /// <summary>
    /// Updates file event record when processing finishes.
    /// Stores Graph item IDs and final success status.
    /// </summary>
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
        // Use DBNull.Value for NULL columns (nullable strings)
        cmd.Parameters.AddWithValue("$DriveId", (object?)driveId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$TempItemId", (object?)tempItemId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$PdfItemId", (object?)pdfItemId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}

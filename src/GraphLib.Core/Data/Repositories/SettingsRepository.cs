using GraphLib.Core.Models;
using GraphLib.Core.Secrets;
using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data.Repositories;

/// <summary>
/// Manages AppSettings table in SQLite.
/// Loads application configuration from the database (row Id=1).
/// Integrates with ISecretProvider for secret resolution/decryption.
/// </summary>
public sealed class SettingsRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ISecretProvider _secretProvider;

    public SettingsRepository(DbConnectionFactory factory, ISecretProvider secretProvider)
    {
        _factory = factory;
        _secretProvider = secretProvider;
    }

    /// <summary>
    /// Loads AppSettings from the database (Id=1).
    /// </summary>
    /// <returns>Populated GraphLibSettings object</returns>
    public GraphLibSettings Get()
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT
  SiteUrl, LibraryName, TempFolder, PdfFolder,
  CleanupTemp, ConflictBehavior,
  StorePdfInSharePoint, ProcessFolderMode, IgnoreFailuresWhenFolderMode,
  TenantId, ClientId, ClientSecret
FROM AppSettings
WHERE Id = 1;
";
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            throw new InvalidOperationException("No AppSettings row found. Run `graphlib init` first.");

        // Parse conflict behavior string to enum
        var conflict = ConflictBehaviorExtensions.Parse(r.GetString(5), ConflictBehavior.Replace);

        // Get raw secret from DB and let ISecretProvider decrypt/resolve it
        var rawSecret = r.GetString(11);
        var resolvedSecret = _secretProvider.GetSecret("ClientSecret", rawSecret);

        // Map database columns to settings object
        // Note: SQLite stores booleans as integers (0/1), so use != 0 to convert
        return new GraphLibSettings
        {
            SiteUrl = r.GetString(0),
            LibraryName = r.GetString(1),
            TempFolder = r.GetString(2),
            PdfFolder = r.GetString(3),

            CleanupTemp = r.GetInt32(4) != 0,
            ConflictBehavior = conflict,

            StorePdfInSharePoint = r.GetInt32(6) != 0,
            ProcessFolderMode = r.GetInt32(7) != 0,
            IgnoreFailuresWhenFolderMode = r.GetInt32(8) != 0,

            TenantId = r.GetString(9),
            ClientId = r.GetString(10),
            ClientSecret = resolvedSecret
        };
    }

    /// <summary>
    /// Updates AppSettings row (Id=1) with new values.
    /// Used when user modifies settings.
    /// </summary>
    public void Update(GraphLibSettings s)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE AppSettings SET
  SiteUrl = $SiteUrl,
  LibraryName = $LibraryName,
  TempFolder = $TempFolder,
  PdfFolder = $PdfFolder,
  CleanupTemp = $CleanupTemp,
  ConflictBehavior = $ConflictBehavior,
  StorePdfInSharePoint = $StorePdfInSharePoint,
  ProcessFolderMode = $ProcessFolderMode,
  IgnoreFailuresWhenFolderMode = $IgnoreFailuresWhenFolderMode,
  TenantId = $TenantId,
  ClientId = $ClientId,
  ClientSecret = $ClientSecret
WHERE Id = 1;
";
        cmd.Parameters.AddWithValue("$SiteUrl", s.SiteUrl);
        cmd.Parameters.AddWithValue("$LibraryName", s.LibraryName);
        cmd.Parameters.AddWithValue("$TempFolder", s.TempFolder);
        cmd.Parameters.AddWithValue("$PdfFolder", s.PdfFolder);
        cmd.Parameters.AddWithValue("$CleanupTemp", s.CleanupTemp ? 1 : 0);
        cmd.Parameters.AddWithValue("$ConflictBehavior", s.ConflictBehavior.ToGraphValue());
        cmd.Parameters.AddWithValue("$StorePdfInSharePoint", s.StorePdfInSharePoint ? 1 : 0);
        cmd.Parameters.AddWithValue("$ProcessFolderMode", s.ProcessFolderMode ? 1 : 0);
        cmd.Parameters.AddWithValue("$IgnoreFailuresWhenFolderMode", s.IgnoreFailuresWhenFolderMode ? 1 : 0);
        cmd.Parameters.AddWithValue("$TenantId", s.TenantId);
        cmd.Parameters.AddWithValue("$ClientId", s.ClientId);
        cmd.Parameters.AddWithValue("$ClientSecret", s.ClientSecret);
        cmd.ExecuteNonQuery();
    }
}

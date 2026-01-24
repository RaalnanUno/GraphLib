using GraphLib.Core.Models;
using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data;

/// <summary>
/// Initializes the SQLite database with schema and default settings.
/// Run once during "graphlib init" command.
/// </summary>
public sealed class DbInitializer
{
    private readonly DbConnectionFactory _factory;

    public DbInitializer(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates the database schema and seeds default AppSettings if they don't exist.
    /// Loads schema from embedded Sql/schema.sql file.
    /// </summary>
    public void EnsureCreatedAndSeedDefaults()
    {
        using var conn = _factory.Open();

        // Load schema.sql from AppContext.BaseDirectory/Sql/schema.sql
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Sql", "schema.sql");
        if (!File.Exists(schemaPath))
            throw new FileNotFoundException($"schema.sql not found at: {schemaPath}");

        // Execute all schema (CREATE TABLE statements)
        var sql = File.ReadAllText(schemaPath);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // Ensure default AppSettings row exists (Id=1)
        EnsureSettingsRow(conn);
    }

    /// <summary>
    /// Ensures AppSettings row with Id=1 exists, inserting defaults if missing.
    /// </summary>
    private static void EnsureSettingsRow(SqliteConnection conn)
    {
        // Check if settings row already exists
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(1) FROM AppSettings WHERE Id = 1;";
        var count = Convert.ToInt32(check.ExecuteScalar());

        if (count > 0) return; // Already exists

        // Get default settings
        var d = GraphLibSettings.DefaultForInit();

        // Insert defaults into AppSettings table
        using var ins = conn.CreateCommand();
        ins.CommandText = @"
INSERT INTO AppSettings
(Id, SiteUrl, LibraryName, TempFolder, PdfFolder, CleanupTemp, ConflictBehavior,
 StorePdfInSharePoint, ProcessFolderMode, IgnoreFailuresWhenFolderMode,
 TenantId, ClientId, ClientSecret)
VALUES
(1, $SiteUrl, $LibraryName, $TempFolder, $PdfFolder, $CleanupTemp, $ConflictBehavior,
 $StorePdfInSharePoint, $ProcessFolderMode, $IgnoreFailuresWhenFolderMode,
 $TenantId, $ClientId, $ClientSecret);";

        // Bind parameters (SQLite uses $ prefix)
        ins.Parameters.AddWithValue("$SiteUrl", d.SiteUrl);
        ins.Parameters.AddWithValue("$LibraryName", d.LibraryName);
        ins.Parameters.AddWithValue("$TempFolder", d.TempFolder);
        ins.Parameters.AddWithValue("$PdfFolder", d.PdfFolder);
        ins.Parameters.AddWithValue("$CleanupTemp", d.CleanupTemp ? 1 : 0); // SQLite has no boolean type, use 1/0
        ins.Parameters.AddWithValue("$ConflictBehavior", d.ConflictBehavior.ToGraphValue());
        ins.Parameters.AddWithValue("$StorePdfInSharePoint", d.StorePdfInSharePoint ? 1 : 0);
        ins.Parameters.AddWithValue("$ProcessFolderMode", d.ProcessFolderMode ? 1 : 0);
        ins.Parameters.AddWithValue("$IgnoreFailuresWhenFolderMode", d.IgnoreFailuresWhenFolderMode ? 1 : 0);
        ins.Parameters.AddWithValue("$TenantId", d.TenantId);
        ins.Parameters.AddWithValue("$ClientId", d.ClientId);
        ins.Parameters.AddWithValue("$ClientSecret", d.ClientSecret);

        ins.ExecuteNonQuery();
    }
}

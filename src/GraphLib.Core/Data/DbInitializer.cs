using GraphLib.Core.Models;
using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data;

public sealed class DbInitializer
{
    private readonly DbConnectionFactory _factory;

    public DbInitializer(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public void EnsureCreatedAndSeedDefaults()
    {
        using var conn = _factory.Open();

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Sql", "schema.sql");
        if (!File.Exists(schemaPath))
            throw new FileNotFoundException($"schema.sql not found at: {schemaPath}");

        var sql = File.ReadAllText(schemaPath);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        EnsureSettingsRow(conn);
    }

    private static void EnsureSettingsRow(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(1) FROM AppSettings WHERE Id = 1;";
        var count = Convert.ToInt32(check.ExecuteScalar());

        if (count > 0) return;

        var d = GraphLibSettings.DefaultForInit();

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

        ins.Parameters.AddWithValue("$SiteUrl", d.SiteUrl);
        ins.Parameters.AddWithValue("$LibraryName", d.LibraryName);
        ins.Parameters.AddWithValue("$TempFolder", d.TempFolder);
        ins.Parameters.AddWithValue("$PdfFolder", d.PdfFolder);
        ins.Parameters.AddWithValue("$CleanupTemp", d.CleanupTemp ? 1 : 0);
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

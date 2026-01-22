using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data;

public sealed class DbConnectionFactory
{
    private readonly string _dbPath;

    public DbConnectionFactory(string dbPath)
    {
        _dbPath = dbPath;
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }
}

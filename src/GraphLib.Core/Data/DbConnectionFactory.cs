using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data;

/// <summary>
/// Factory for creating SQLite database connections.
/// Each call to Open() returns a new connection (no connection pooling).
/// Connections should be disposed using 'using' statement.
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly string _dbPath;

    /// <summary>
    /// Initializes factory with database file path.
    /// </summary>
    /// <param name="dbPath">Absolute path to the SQLite database file</param>
    public DbConnectionFactory(string dbPath)
    {
        _dbPath = dbPath;
    }

    /// <summary>
    /// Opens and returns a new SQLite connection.
    /// Caller is responsible for disposing the connection.
    /// </summary>
    public SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }
}

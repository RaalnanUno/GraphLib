using Microsoft.Data.Sqlite;

namespace GraphLib.Core.Data;

/// <summary>
/// Factory for creating SQLite database connections.
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
    /// Creates a new SQLite connection (NOT opened).
    /// Use this for async open patterns.
    /// </summary>
    public SqliteConnection Create()
        => new($"Data Source={_dbPath}");

    /// <summary>
    /// Opens and returns a new SQLite connection (sync open).
    /// Caller is responsible for disposing the connection.
    /// </summary>
    public SqliteConnection Open()
    {
        var conn = Create();
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Provides a factory compatible with repositories expecting "create new connection".
    /// The returned connections are NOT opened.
    /// </summary>
    public Func<SqliteConnection> CreateFactory() => () => Create();
}

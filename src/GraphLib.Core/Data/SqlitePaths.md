namespace GraphLib.Core.Data;

/// <summary>
/// Resolves relative database paths to absolute paths.
/// Handles path resolution and directory creation.
/// </summary>
public static class SqlitePaths
{
    /// <summary>
    /// Resolves a database path, creating directories as needed.
    /// </summary>
    /// <param name="rawPath">User-supplied path (can be relative or absolute, nullable)</param>
    /// <param name="defaultRelativeToExe">Default path relative to executable if rawPath is not provided</param>
    /// <returns>Absolute path to database file</returns>
    public static string ResolveDbPath(string? rawPath, string defaultRelativeToExe = "./Data/GraphLib.db")
    {
        // Use provided path or default
        var p = string.IsNullOrWhiteSpace(rawPath) ? defaultRelativeToExe : rawPath.Trim();

        // Convert relative path to absolute (relative to executable directory)
        if (!Path.IsPathRooted(p))
            p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, p));

        // Ensure directory exists
        var dir = Path.GetDirectoryName(p);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        return p;
    }
}

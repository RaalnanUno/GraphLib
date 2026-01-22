namespace GraphLib.Core.Data;

public static class SqlitePaths
{
    public static string ResolveDbPath(string? rawPath, string defaultRelativeToExe = "./Data/GraphLib.db")
    {
        var p = string.IsNullOrWhiteSpace(rawPath) ? defaultRelativeToExe : rawPath.Trim();

        if (!Path.IsPathRooted(p))
            p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, p));

        var dir = Path.GetDirectoryName(p);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        return p;
    }
}

namespace GraphLib.Core.Models;

public enum ConflictBehavior
{
    Fail,
    Replace,
    Rename
}

public static class ConflictBehaviorExtensions
{
    public static ConflictBehavior Parse(string? s, ConflictBehavior @default = ConflictBehavior.Replace)
    {
        if (string.IsNullOrWhiteSpace(s)) return @default;

        return s.Trim().ToLowerInvariant() switch
        {
            "fail" => ConflictBehavior.Fail,
            "replace" => ConflictBehavior.Replace,
            "rename" => ConflictBehavior.Rename,
            _ => @default
        };
    }

    public static string ToGraphValue(this ConflictBehavior b) =>
        b switch
        {
            ConflictBehavior.Fail => "fail",
            ConflictBehavior.Replace => "replace",
            ConflictBehavior.Rename => "rename",
            _ => "replace"
        };
}

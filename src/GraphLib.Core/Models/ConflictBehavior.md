namespace GraphLib.Core.Models;

/// <summary>
/// Determines how to handle filename conflicts in SharePoint.
/// Applied when uploading files with the same name.
/// </summary>
public enum ConflictBehavior
{
    /// <summary>Raise an error if the file already exists</summary>
    Fail,
    
    /// <summary>Overwrite the existing file with the new one</summary>
    Replace,
    
    /// <summary>Rename the new file (e.g., "Document (1).docx")</summary>
    Rename
}

/// <summary>
/// Extension methods for ConflictBehavior enum.
/// </summary>
public static class ConflictBehaviorExtensions
{
    /// <summary>
    /// Parses a string to ConflictBehavior enum value (case-insensitive).
    /// </summary>
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

    /// <summary>
    /// Converts ConflictBehavior enum to Microsoft Graph API value string.
    /// </summary>
    public static string ToGraphValue(this ConflictBehavior b) =>
        b switch
        {
            ConflictBehavior.Fail => "fail",
            ConflictBehavior.Replace => "replace",
            ConflictBehavior.Rename => "rename",
            _ => "replace"
        };
}

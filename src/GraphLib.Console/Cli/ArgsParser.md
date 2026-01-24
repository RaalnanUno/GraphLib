namespace GraphLib.ConsoleApp.Cli;

/// <summary>
/// Parses command-line arguments into an Args object.
/// Handles both positional commands (e.g., "init", "run") and named options (e.g., "--file path.docx").
/// </summary>
public static class ArgsParser
{
    /// <summary>
    /// Parses command-line arguments.
    /// 
    /// Expected formats:
    /// - "init --db ./Data/GraphLib.db"
    /// - "run --file C:\path\file.docx --cleanup true"
    /// - "help"
    /// 
    /// The first argument is treated as a command if it doesn't start with "--".
    /// All subsequent arguments must be in the form --key value (or --key for flags).
    /// </summary>
    public static Args Parse(string[] argv)
    {
        var a = new Args();

        // Check if first argument is a command (like "init" or "run") or an option flag
        // Commands don't start with "--", so "init" is a command, but "--file" is an option
        var startIndex = 0;
        if (argv.Length > 0 && !argv[0].StartsWith("--"))
        {
            a.Command = argv[0].Trim().ToLowerInvariant();
            startIndex = 1;  // Skip the command, parse options starting from index 1
        }

        // Iterate through all arguments, parsing named options (--key value pairs)
        for (int i = startIndex; i < argv.Length; i++)
        {
            var token = argv[i];
            // Only process tokens that start with "--"; skip anything else
            if (!token.StartsWith("--")) continue;

            // Extract the key name by removing the "--" prefix
            var key = token.Substring(2);
            
            // Extract the value: if the next token exists and doesn't start with "--", it's the value
            // Otherwise, default to "true" (for boolean flags like --cleanup without a value)
            // Extract the value: if the next token exists and doesn't start with "--", it's the value
            // Otherwise, default to "true" (for boolean flags like --cleanup without a value)
            var val = (i + 1 < argv.Length && !argv[i + 1].StartsWith("--")) ? argv[++i] : "true";

            // Map each option key to its corresponding property in the Args object
            // Use case-insensitive matching to allow --File or --FILE
            switch (key.ToLowerInvariant())
            {
                // Core arguments
                case "file": a.File = val; break;
                case "db": a.Db = val; break;
                
                // SharePoint configuration
                case "siteurl": a.SiteUrl = val; break;
                case "libraryname": a.LibraryName = val; break;
                case "tempfolder": a.TempFolder = val; break;
                case "pdffolder": a.PdfFolder = val; break;
                
                // Behavior settings
                case "cleanuptemp": a.CleanupTemp = ParseBool(val); break;
                case "conflictbehavior": a.ConflictBehavior = val; break;
                case "runid": a.RunId = val; break;
                case "logfailuresonly": a.LogFailuresOnly = ParseBool(val); break;

                // Advanced settings
                case "storepdfinsharepoint": a.StorePdfInSharePoint = ParseBool(val); break;
                case "processfoldermode": a.ProcessFolderMode = ParseBool(val); break;
                case "ignorefailureswhenfoldermode": a.IgnoreFailuresWhenFolderMode = ParseBool(val); break;

                // Azure AD authentication
                case "tenantid": a.TenantId = val; break;
                case "clientid": a.ClientId = val; break;
                case "clientsecret": a.ClientSecret = val; break;
            }
        }

        return a;
    }

    /// <summary>
    /// Converts a string value to a boolean.
    /// 
    /// Accepts multiple formats for true:  "1", "true", "yes", "y"
    /// Accepts multiple formats for false: "0", "false", "no", "n"
    /// Default: returns true for any unrecognized value (permissive parsing)
    /// 
    /// This allows flexibility in command-line usage:
    /// --cleanuptemp true
    /// --cleanuptemp 1
    /// --cleanuptemp yes
    /// --cleanuptemp  (defaults to true)
    /// </summary>
    private static bool ParseBool(string s)
        => s.Trim().ToLowerInvariant() switch
        {
            // Accept various truthy values
            "1" => true,
            "true" => true,
            "yes" => true,
            "y" => true,
            
            // Accept various falsy values
            "0" => false,
            "false" => false,
            "no" => false,
            "n" => false,
            
            // Default: if user doesn't explicitly set false, treat as true
            // This handles cases like "--flag" with no value following it
            _ => true
        };
}

namespace GraphLib.ConsoleApp.Cli;

public static class ArgsParser
{
    public static Args Parse(string[] argv)
    {
        var a = new Args();

        // Determine whether argv[0] is a command or an option
        var startIndex = 0;
        if (argv.Length > 0 && !argv[0].StartsWith("--"))
        {
            a.Command = argv[0].Trim().ToLowerInvariant();
            startIndex = 1;
        }

        for (int i = startIndex; i < argv.Length; i++)
        {
            var token = argv[i];
            if (!token.StartsWith("--")) continue;

            var key = token.Substring(2);
            var val = (i + 1 < argv.Length && !argv[i + 1].StartsWith("--")) ? argv[++i] : "true";

            switch (key.ToLowerInvariant())
            {
                case "file": a.File = val; break;
                case "db": a.Db = val; break;
                case "siteurl": a.SiteUrl = val; break;
                case "libraryname": a.LibraryName = val; break;
                case "tempfolder": a.TempFolder = val; break;
                case "pdffolder": a.PdfFolder = val; break;
                case "cleanuptemp": a.CleanupTemp = ParseBool(val); break;
                case "conflictbehavior": a.ConflictBehavior = val; break;
                case "runid": a.RunId = val; break;
                case "logfailuresonly": a.LogFailuresOnly = ParseBool(val); break;

                case "storepdfinsharepoint": a.StorePdfInSharePoint = ParseBool(val); break;
                case "processfoldermode": a.ProcessFolderMode = ParseBool(val); break;
                case "ignorefailureswhenfoldermode": a.IgnoreFailuresWhenFolderMode = ParseBool(val); break;

                case "tenantid": a.TenantId = val; break;
                case "clientid": a.ClientId = val; break;
                case "clientsecret": a.ClientSecret = val; break;
            }
        }

        return a;
    }

    private static bool ParseBool(string s)
        => s.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "y" => true,
            "0" => false,
            "false" => false,
            "no" => false,
            "n" => false,
            _ => true
        };
}

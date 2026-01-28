using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraphLib.PdfConsoleNet48
{
    internal static class Program
    {
        /// <summary>
        /// Usage:
        ///   GraphLib.PdfConsoleNet48.exe "C:\path\file.docx" ["C:\path\out.pdf"]
        ///
        /// Settings come from graphlib.settings.props.txt which is generated from .csproj properties.
        /// </summary>
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.WriteLine("Usage: GraphLib.PdfConsoleNet48.exe <inputFile> [outputPdf]");
                return 2;
            }

            var inputPath = args[0];
            var outputPath = args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1])
                ? args[1]
                : Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? ".",
                    Path.GetFileNameWithoutExtension(inputPath) + ".pdf");

            if (!File.Exists(inputPath))
            {
                Console.WriteLine("Input file not found: " + inputPath);
                return 2;
            }

            var propsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "graphlib.settings.props.txt");
            if (!File.Exists(propsPath))
            {
                Console.WriteLine("Missing settings file: " + propsPath);
                Console.WriteLine("Build the project so the .csproj target generates it.");
                return 2;
            }

            var props = LoadProps(propsPath);

            var settings = new GraphLibSettings
            {
                TenantId = Get(props, "TenantId"),
                ClientId = Get(props, "ClientId"),
                ClientSecret = Get(props, "ClientSecret"),
                SiteUrl = Get(props, "SiteUrl"),
                LibraryName = Get(props, "LibraryName"),
                TempFolder = Get(props, "TempFolder"),
                ConflictBehavior = ConflictBehaviorExtensions.Parse(Get(props, "ConflictBehavior"))
            };

            var enableSqlite = string.Equals(Get(props, "EnableSqliteLogging"), "true", StringComparison.OrdinalIgnoreCase);
            var sqlitePath = Get(props, "SqliteDbPath");

            IGraphLibErrorLogger logger = null;
            if (enableSqlite)
            {
                // If you enable this, go into GraphLibSqliteErrorLogger and uncomment the SQLite code.
                logger = string.IsNullOrWhiteSpace(sqlitePath)
                    ? new GraphLibSqliteErrorLogger()
                    : new GraphLibSqliteErrorLogger(sqlitePath);
            }

            // Joke for the devs:
            // If this app crashes, it wasn't a bug — it was an "unexpected feature preview".

            try
            {
                using (var runner = new GraphLibPdfRunner(settings, logger))
                {
                    var result = await runner.ConvertFileToPdfAsync(inputPath, CancellationToken.None);

                    foreach (var log in result.Logs)
                        Console.WriteLine($"{log.Utc:u} {log.Level,-5} {log.Stage,-14} {log.Message}");

                    if (!result.Success)
                    {
                        Console.WriteLine(result.Summary);
                        return 1;
                    }

                    File.WriteAllBytes(outputPath, result.PdfBytes);

                    Console.WriteLine();
                    Console.WriteLine("OK");
                    Console.WriteLine("RunId     : " + result.RunId);
                    Console.WriteLine("InputBytes: " + result.InputBytes);
                    Console.WriteLine("PdfBytes  : " + result.PdfBytesLength);
                    Console.WriteLine("Output    : " + outputPath);
                    Console.WriteLine("ElapsedMs : " + (int)result.Elapsed.TotalMilliseconds);

                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL (" + ex.GetType().Name + "): " + ex.Message);
                return 1;
            }
        }

        private static Dictionary<string, string> LoadProps(string path)
        {
            return File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                .Select(l =>
                {
                    var i = l.IndexOf('=');
                    return i > 0 ? new[] { l.Substring(0, i).Trim(), l.Substring(i + 1).Trim() } : new[] { l, "" };
                })
                .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
        }

        private static string Get(Dictionary<string, string> props, string key)
            => props.TryGetValue(key, out var v) ? v : "";
    }
}

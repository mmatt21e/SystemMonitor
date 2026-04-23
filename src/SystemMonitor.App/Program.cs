using System.Runtime.Versioning;
using System.Windows.Forms;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Logging;

namespace SystemMonitor.App;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintHelp();
            return 0;
        }

        var verifyTarget = GetArg(args, "--verify");
        if (verifyTarget is not null)
            return RunVerify(verifyTarget);

        var configPath = GetArg(args, "--config") ?? "config.json";
        AppConfig config;
        try
        {
            var (cfg, _) = ConfigLoader.LoadOrDefaults(configPath);
            config = cfg;
        }
        catch (ConfigLoadException ex)
        {
            Console.Error.WriteLine($"Config error: {ex.Message}");
            return 2;
        }

        var output = GetArg(args, "--output");
        if (output is not null) config.LogOutputDirectory = output;

        if (HasFlag(args, "--headless"))
            return RunHeadless(config);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(config));
        return 0;
    }

    private static int RunVerify(string target)
    {
        var files = Directory.Exists(target)
            ? Directory.GetFiles(target, "*.jsonl", SearchOption.TopDirectoryOnly)
            : File.Exists(target) ? new[] { target }
            : Array.Empty<string>();

        if (files.Length == 0)
        {
            Console.Error.WriteLine($"No .jsonl files found at '{target}'.");
            return 2;
        }

        int failures = 0;
        foreach (var file in files)
        {
            var result = LogVerifier.Verify(file);
            if (result.Ok)
            {
                Console.WriteLine($"OK     {file}  ({result.LinesChecked} lines)");
            }
            else
            {
                failures++;
                if (result.FirstFailureLine is int ln)
                    Console.WriteLine($"FAIL   {file}  line {ln} @ byte {result.FirstFailureByteOffset}  {result.Error}");
                else
                    Console.WriteLine($"ERROR  {file}  {result.Error}");
            }
        }

        return failures == 0 ? 0 : 3;
    }

    private static int RunHeadless(AppConfig config)
    {
        Console.WriteLine($"SystemMonitor headless — logging to {config.LogOutputDirectory}");
        using var host = EngineHost.Build(config);
        host.OnAnomaly += ev => Console.WriteLine(
            $"[{ev.Timestamp:HH:mm:ss}] {ev.Classification,-14} {ev.Summary}");

        host.Start();

        var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        Console.WriteLine("Running. Press Ctrl+C to stop.");
        stop.Wait();

        Console.WriteLine("Stopping...");
        host.Stop();
        Console.WriteLine("Done.");
        return 0;
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    private static void PrintHelp() => Console.WriteLine(
        """
        SystemMonitor — diagnostic tool

        Usage:
          SystemMonitor.exe [--config <path>] [--output <dir>] [--headless]
          SystemMonitor.exe --verify <path-or-dir>

        Options:
          --config <path>   Path to config.json (default: ./config.json; defaults used if absent)
          --output <dir>    Override LogOutputDirectory from config
          --headless        Run without UI; log to disk until Ctrl+C
          --verify <path>   Verify the HMAC chain of one .jsonl file or every .jsonl in a directory
          --help, -h        Show this help
        """);
}

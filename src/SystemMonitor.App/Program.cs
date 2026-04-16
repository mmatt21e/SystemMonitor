using System.Runtime.Versioning;
using System.Windows.Forms;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;

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

        Options:
          --config <path>   Path to config.json (default: ./config.json; defaults used if absent)
          --output <dir>    Override LogOutputDirectory from config
          --headless        Run without UI; log to disk until Ctrl+C
          --help, -h        Show this help
        """);
}

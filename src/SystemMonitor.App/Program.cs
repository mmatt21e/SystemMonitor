using System.Runtime.Versioning;
using System.Windows.Forms;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.App;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var configPath = GetArg(args, "--config") ?? "config.json";
        var (config, _) = ConfigLoader.LoadOrDefaults(configPath);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(config));
        return 0;
    }

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}

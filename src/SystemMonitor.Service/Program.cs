using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.Service;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private const string ServiceName = "SystemMonitor";

    // Program.ConfigPath and Program.LogDirectory are deliberately under %ProgramData%
    // so they are writable by LocalSystem (the service account) and not tied to any
    // user profile. The MSI lays down config.example.json next to the exe; the user
    // (or MDM policy) copies/edits it to %ProgramData%\SystemMonitor\config.json.
    private static readonly string ProgramDataRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SystemMonitor");

    private static readonly string ConfigPath = Path.Combine(ProgramDataRoot, "config.json");
    private static readonly string DefaultLogDir = Path.Combine(ProgramDataRoot, "Logs");

    private static int Main(string[] args)
    {
        Directory.CreateDirectory(ProgramDataRoot);
        Directory.CreateDirectory(DefaultLogDir);

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = ServiceName;
        });

        builder.Logging.AddEventLog(settings =>
        {
            settings.SourceName = ServiceName;
        });

        builder.Services.AddSingleton<Func<IEngineLifetime>>(_ =>
        {
            return () =>
            {
                var (config, source) = ConfigLoader.LoadOrDefaults(ConfigPath);
                // Force output dir under ProgramData unless the user explicitly overrode it
                // to something writable by LocalSystem.
                if (string.IsNullOrWhiteSpace(config.LogOutputDirectory))
                    config.LogOutputDirectory = DefaultLogDir;

                return EngineHost.Build(config);
            };
        });

        builder.Services.AddHostedService<EngineWorker>();

        var host = builder.Build();
        host.Run();
        return 0;
    }
}

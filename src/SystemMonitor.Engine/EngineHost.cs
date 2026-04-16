using System.Runtime.Versioning;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Collectors.Lhm;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using SystemMonitor.Engine.Logging;

namespace SystemMonitor.Engine;

/// <summary>
/// Composes the engine: collectors + buffers + logger + correlation. Exposes live state
/// for the UI (buffers, anomaly stream, capability report) and can be used in headless mode too.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EngineHost : IDisposable
{
    public AppConfig Config { get; }
    public bool IsAdministrator { get; }
    public LhmComputer? Lhm { get; }
    public IReadOnlyDictionary<string, ReadingRingBuffer> Buffers => _buffers;
    public IReadOnlyList<ICollector> Collectors => _collectors;
    public event Action<Reading>? OnReading;
    public event Action<AnomalyEvent>? OnAnomaly;
#pragma warning disable CS0067 // OnDiagnostic is declared for future wiring of collector failure streams.
    public event Action<string>? OnDiagnostic;
#pragma warning restore CS0067

    private readonly Dictionary<string, ReadingRingBuffer> _buffers;
    private readonly List<ICollector> _collectors;
    private readonly JsonlLogger _readingsLog;
    private readonly JsonlLogger _eventsLog;
    private readonly JsonlLogger _anomaliesLog;
    private readonly Orchestrator _orchestrator;
    private readonly CorrelationEngine _correlation;
    private volatile bool _disposed;

    private EngineHost(
        AppConfig config, bool isAdmin, LhmComputer? lhm,
        Dictionary<string, ReadingRingBuffer> buffers, List<ICollector> collectors,
        JsonlLogger readingsLog, JsonlLogger eventsLog, JsonlLogger anomaliesLog,
        Orchestrator orchestrator, CorrelationEngine correlation)
    {
        Config = config;
        IsAdministrator = isAdmin;
        Lhm = lhm;
        _buffers = buffers;
        _collectors = collectors;
        _readingsLog = readingsLog;
        _eventsLog = eventsLog;
        _anomaliesLog = anomaliesLog;
        _orchestrator = orchestrator;
        _correlation = correlation;
    }

    public static EngineHost Build(AppConfig config)
    {
        bool isAdmin = PrivilegeDetector.IsAdministrator();

        LhmComputer? lhm = null;
        if (isAdmin)
        {
            try { lhm = LhmComputer.Open(); }
            catch { lhm = null; }
        }

        var collectors = new List<ICollector>();
        if (Enabled(config, "cpu"))         collectors.Add(new CpuCollector(Ms(config, "cpu"), lhm));
        if (Enabled(config, "memory"))      collectors.Add(new MemoryCollector(Ms(config, "memory")));
        if (Enabled(config, "storage"))     collectors.Add(new StorageCollector(Ms(config, "storage")));
        if (Enabled(config, "network"))     collectors.Add(new NetworkCollector(Ms(config, "network")));
        if (Enabled(config, "power"))       collectors.Add(new PowerCollector(Ms(config, "power"), lhm));
        if (Enabled(config, "gpu"))         collectors.Add(new GpuCollector(Ms(config, "gpu"), lhm));
        if (Enabled(config, "eventlog"))    collectors.Add(new EventLogCollector(Ms(config, "eventlog")));
        if (Enabled(config, "reliability")) collectors.Add(new ReliabilityCollector(Ms(config, "reliability"), config.WmiTimeoutMs));
        if (Enabled(config, "inventory"))   collectors.Add(new InventoryCollector(config.WmiTimeoutMs));

        var buffers = collectors.ToDictionary(c => c.Name,
            _ => new ReadingRingBuffer(config.BufferCapacityPerCollector));

        var readingsLog = new JsonlLogger(config.LogOutputDirectory, "readings", config.LogRotationSizeBytes);
        var eventsLog = new JsonlLogger(config.LogOutputDirectory, "events", config.LogRotationSizeBytes);
        var anomaliesLog = new JsonlLogger(config.LogOutputDirectory, "anomalies", config.LogRotationSizeBytes);

        WriteCapabilityHeader(readingsLog, isAdmin, collectors);
        WriteCapabilityHeader(eventsLog, isAdmin, collectors);
        WriteCapabilityHeader(anomaliesLog, isAdmin, collectors);

        var rules = new List<ICorrelationRule>
        {
            new ThermalRunawayRule(),
            new PowerAndKernelPowerRule(),
            new DiskLatencyAndSmartRule(),
            new NetworkDropAndPacketLossRule(),
            new BaselineDeviationRule("cpu", "temperature_celsius"),
            new BaselineDeviationRule("cpu", "usage_percent"),
        };

        var host = (EngineHost?)null;   // forward reference for closures

        var orchestrator = new Orchestrator(collectors, buffers, r =>
        {
            if (host is null || host._disposed) return;
            host.OnReading?.Invoke(r);
            if (r.Source == "eventlog") eventsLog.WriteReading(r);
            else readingsLog.WriteReading(r);
        });

        var correlation = new CorrelationEngine(rules, buffers, config.Thresholds, ev =>
        {
            if (host is null || host._disposed) return;
            host.OnAnomaly?.Invoke(ev);
            anomaliesLog.WriteLine(System.Text.Json.JsonSerializer.Serialize(ev));
            anomaliesLog.Flush();
        });

        host = new EngineHost(config, isAdmin, lhm, buffers, collectors, readingsLog, eventsLog, anomaliesLog,
                              orchestrator, correlation);
        return host;
    }

    public void Start()
    {
        _orchestrator.Start();
        _correlation.Start(TimeSpan.FromMilliseconds(Config.CorrelationIntervalMs));
    }

    public void Stop()
    {
        _orchestrator.Stop();
        _correlation.Stop();
        _readingsLog.Flush();
        _eventsLog.Flush();
        _anomaliesLog.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        // Allow any in-flight collector/correlation timer callbacks to exit before tearing down loggers.
        Thread.Sleep(100);
        _readingsLog.Dispose();
        _eventsLog.Dispose();
        _anomaliesLog.Dispose();
        Lhm?.Dispose();
        foreach (var c in _collectors.OfType<IDisposable>()) c.Dispose();
    }

    private static bool Enabled(AppConfig c, string name)
        => c.Collectors.TryGetValue(name, out var cc) && cc.Enabled;

    private static TimeSpan Ms(AppConfig c, string name)
        => TimeSpan.FromMilliseconds(c.Collectors[name].PollingIntervalMs);

    private static void WriteCapabilityHeader(JsonlLogger log, bool isAdmin, IEnumerable<ICollector> collectors)
    {
        var header = new
        {
            type = "capability_report",
            timestamp = DateTimeOffset.UtcNow,
            is_administrator = isAdmin,
            machine = Environment.MachineName,
            collectors = collectors.Select(c => new
            {
                name = c.Name,
                capability_level = c.Capability.Level.ToString(),
                reason = c.Capability.Reason,
                polling_interval_ms = (int)c.PollingInterval.TotalMilliseconds
            })
        };
        log.WriteLine(System.Text.Json.JsonSerializer.Serialize(header));
        log.Flush();
    }
}

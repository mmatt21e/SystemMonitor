using System.Management;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class InventoryCollector : CollectorBase
{
    private readonly int _wmiTimeoutMs;

    // PollingInterval == Zero signals the Orchestrator to fire once and not repeat.
    public InventoryCollector(int wmiTimeoutMs) : base("inventory", TimeSpan.Zero)
    {
        _wmiTimeoutMs = wmiTimeoutMs;
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        yield return Info("machine_name", Environment.MachineName);
        yield return Info("os_version", Environment.OSVersion.VersionString);
        yield return Info("processor_count", Environment.ProcessorCount.ToString());
        yield return Info("clr_version", Environment.Version.ToString());

        foreach (var r in WmiInventory("SELECT Name, Manufacturer, NumberOfCores, MaxClockSpeed FROM Win32_Processor", "cpu_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Manufacturer, Product, Version FROM Win32_BaseBoard", "motherboard_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Manufacturer, Name, Version, ReleaseDate FROM Win32_BIOS", "bios_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory", "ram_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Model, InterfaceType, Size, MediaType FROM Win32_DiskDrive", "disk_info", ts))
            yield return r;
    }

    private Reading Info(string metric, string value) =>
        new("inventory", metric, 1, "info", DateTimeOffset.UtcNow, ReadingConfidence.High,
            new Dictionary<string, string> { ["value"] = value });

    private IEnumerable<Reading> WmiInventory(string query, string metric, DateTimeOffset ts)
    {
        List<Reading> results = new();
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\cimv2", query);
            searcher.Options.Timeout = TimeSpan.FromMilliseconds(_wmiTimeoutMs);
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    var labels = new Dictionary<string, string>();
                    foreach (var prop in mo.Properties)
                    {
                        if (prop.Value is null) continue;
                        labels[prop.Name] = prop.Value.ToString() ?? "";
                    }
                    results.Add(new Reading("inventory", metric, 1, "info", ts, ReadingConfidence.High, labels));
                }
            }
        }
        catch { }
        return results;
    }
}

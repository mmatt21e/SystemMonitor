using System.Management;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class ReliabilityCollector : CollectorBase
{
    private readonly int _wmiTimeoutMs;

    public ReliabilityCollector(TimeSpan pollingInterval, int wmiTimeoutMs) : base("reliability", pollingInterval)
    {
        _wmiTimeoutMs = wmiTimeoutMs;
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;
        var results = new List<Reading>();

        // Win32_ReliabilityRecords — Windows' rollup of stability events.
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT SourceName, EventIdentifier, TimeGenerated, Message, ProductName FROM Win32_ReliabilityRecords");
            searcher.Options.Timeout = TimeSpan.FromMilliseconds(_wmiTimeoutMs);

            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    var labels = new Dictionary<string, string>
                    {
                        ["source"] = mo["SourceName"]?.ToString() ?? "",
                        ["event_id"] = mo["EventIdentifier"]?.ToString() ?? "",
                        ["product"] = mo["ProductName"]?.ToString() ?? "",
                        ["message"] = Truncate(mo["Message"]?.ToString() ?? "", 400)
                    };
                    var when = ManagementDateTimeConverter.ToDateTime(mo["TimeGenerated"]?.ToString() ?? "");
                    results.Add(new Reading("reliability", "record", 1, "count",
                        new DateTimeOffset(when.ToUniversalTime(), TimeSpan.Zero),
                        ReadingConfidence.High, labels));
                }
            }
        }
        catch { /* missing class on older systems, insufficient privilege, etc. */ }

        // Minidump directory inventory.
        var minidumpDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
        if (Directory.Exists(minidumpDir))
        {
            foreach (var f in Directory.EnumerateFiles(minidumpDir, "*.dmp"))
            {
                var fi = new FileInfo(f);
                results.Add(new Reading("reliability", "minidump", fi.Length, "bytes",
                    new DateTimeOffset(fi.CreationTimeUtc, TimeSpan.Zero), ReadingConfidence.High,
                    new Dictionary<string, string>
                    {
                        ["path"] = f,
                        ["filename"] = fi.Name
                    }));
            }
        }

        return results;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}

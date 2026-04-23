using System.Management;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Diagnostics;

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

        // Minidump directory inventory, enriched with BugCheck analysis when we can
        // parse the DUMP_HEADER64. A kernel dump without bugcheck fields is still
        // reported — just without the diagnostic labels.
        var minidumpDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
        if (Directory.Exists(minidumpDir))
        {
            foreach (var f in Directory.EnumerateFiles(minidumpDir, "*.dmp"))
            {
                var fi = new FileInfo(f);
                var labels = new Dictionary<string, string>
                {
                    ["path"] = f,
                    ["filename"] = fi.Name
                };

                var info = MinidumpReader.TryRead(f);
                if (info is not null)
                {
                    labels["bugcheck_code"] = $"0x{info.BugCheckCode:X8}";
                    labels["bugcheck_name"] = info.BugCheckName;
                    labels["bugcheck_p1"] = $"0x{info.BugCheckParameter1:X16}";
                    labels["bugcheck_p2"] = $"0x{info.BugCheckParameter2:X16}";
                    labels["bugcheck_p3"] = $"0x{info.BugCheckParameter3:X16}";
                    labels["bugcheck_p4"] = $"0x{info.BugCheckParameter4:X16}";
                }

                results.Add(new Reading("reliability", "minidump", fi.Length, "bytes",
                    new DateTimeOffset(fi.CreationTimeUtc, TimeSpan.Zero), ReadingConfidence.High,
                    labels));
            }
        }

        return results;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}

using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

/// <summary>
/// Tails Windows event logs and emits a reading per relevant entry.
/// Relevant = warning/error/critical level entries on System, Application, and
/// Hardware Events channels, published since the last poll (or within <paramref name="lookback"/>
/// on first poll).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EventLogCollector : CollectorBase
{
    private static readonly string[] Channels = { "System", "Application", "Microsoft-Windows-Kernel-WHEA/Errors" };
    private readonly TimeSpan _lookback;
    private DateTime _sinceUtc;

    public EventLogCollector(TimeSpan pollingInterval, TimeSpan? lookback = null)
        : base("eventlog", pollingInterval)
    {
        _lookback = lookback ?? TimeSpan.FromMinutes(10);
        _sinceUtc = DateTime.UtcNow - _lookback;
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var now = DateTime.UtcNow;
        var results = new List<Reading>();

        foreach (var channel in Channels)
        {
            try
            {
                // XPath query: level <= 3 (Critical/Error/Warning) and time >= since.
                var xpath = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[@SystemTime>='{_sinceUtc:O}']]]";
                var query = new EventLogQuery(channel, PathType.LogName, xpath) { ReverseDirection = false };
                using var reader = new EventLogReader(query);
                for (var ev = reader.ReadEvent(); ev != null; ev = reader.ReadEvent())
                {
                    using (ev)
                    {
                        var ts = ev.TimeCreated is { } tc ? new DateTimeOffset(tc.ToUniversalTime()) : DateTimeOffset.UtcNow;
                        var labels = new Dictionary<string, string>
                        {
                            ["channel"] = channel,
                            ["event_id"] = ev.Id.ToString(),
                            ["level"] = ev.LevelDisplayName ?? ev.Level?.ToString() ?? "Unknown",
                            ["provider"] = ev.ProviderName ?? ""
                        };
                        var message = SafeFormat(ev);
                        if (!string.IsNullOrWhiteSpace(message)) labels["message"] = Truncate(message, 400);

                        // Value encodes level: 1=Critical, 2=Error, 3=Warning.
                        results.Add(new Reading("eventlog", "event", ev.Level ?? 0, "level", ts,
                            ReadingConfidence.High, labels));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Security channel etc. — skip quietly; capability report already reflects access.
            }
            catch (EventLogNotFoundException)
            {
                // Hardware Events channel may not exist on older systems.
            }
        }

        _sinceUtc = now;
        return results;
    }

    private static string SafeFormat(EventRecord ev)
    {
        try { return ev.FormatDescription() ?? ""; }
        catch { return ""; }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}

using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

public interface ICollector
{
    string Name { get; }
    TimeSpan PollingInterval { get; }
    CapabilityStatus Capability { get; }

    /// <summary>
    /// Produces readings for this tick. Implementations MUST NOT throw —
    /// the base class wraps the concrete collector in a try/catch cooldown/retry loop.
    /// </summary>
    IReadOnlyList<Reading> Collect();
}

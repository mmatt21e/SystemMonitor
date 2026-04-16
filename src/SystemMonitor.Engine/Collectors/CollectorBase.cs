using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

public abstract class CollectorBase : ICollector
{
    // After this many consecutive failures, the collector enters cooldown.
    private const int FailureThreshold = 3;

    // Cooldown duration before retry is allowed.
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(60);

    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

    protected CollectorBase(string name, TimeSpan pollingInterval)
    {
        Name = name;
        PollingInterval = pollingInterval;
    }

    public string Name { get; }
    public TimeSpan PollingInterval { get; }
    public abstract CapabilityStatus Capability { get; }

    public int ConsecutiveFailures { get; private set; }
    public bool IsCooldownActive => DateTimeOffset.UtcNow < _cooldownUntil;

    /// <summary>Concrete collectors implement this; may throw.</summary>
    protected abstract IEnumerable<Reading> CollectCore();

    public IReadOnlyList<Reading> Collect()
    {
        if (IsCooldownActive) return Array.Empty<Reading>();

        try
        {
            var result = CollectCore().ToList();
            ConsecutiveFailures = 0;
            return result;
        }
        catch (Exception ex)
        {
            ConsecutiveFailures++;
            OnFailure(ex);
            if (ConsecutiveFailures >= FailureThreshold)
                _cooldownUntil = DateTimeOffset.UtcNow + CooldownDuration;
            return Array.Empty<Reading>();
        }
    }

    /// <summary>Override to surface collector-specific failure to the diagnostics log.</summary>
    protected virtual void OnFailure(Exception ex) { }
}

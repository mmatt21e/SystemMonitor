namespace SystemMonitor.Engine.Capabilities;

public enum CapabilityLevel
{
    Full,
    Partial,
    Unavailable
}

/// <summary>
/// Describes whether a collector/sensor source is usable, and if not, why.
/// Surfaced in the capability report at the top of every log file and in the UI.
/// </summary>
public sealed record CapabilityStatus(CapabilityLevel Level, string? Reason)
{
    public static CapabilityStatus Full() => new(CapabilityLevel.Full, null);
    public static CapabilityStatus Partial(string reason) => new(CapabilityLevel.Partial, reason);
    public static CapabilityStatus Unavailable(string reason) => new(CapabilityLevel.Unavailable, reason);
}

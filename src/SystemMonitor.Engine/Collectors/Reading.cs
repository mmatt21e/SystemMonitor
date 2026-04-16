namespace SystemMonitor.Engine.Collectors;

public enum ReadingConfidence
{
    Low,
    Medium,
    High
}

/// <summary>
/// An immutable sensor/metric reading. Produced by collectors, consumed by the
/// ring buffer, logger, correlation engine, and UI.
/// </summary>
public sealed record Reading(
    string Source,
    string Metric,
    double Value,
    string Unit,
    DateTimeOffset Timestamp,
    ReadingConfidence Confidence,
    IReadOnlyDictionary<string, string> Labels);

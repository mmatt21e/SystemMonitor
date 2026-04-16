using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.Engine.Correlation;

/// <summary>
/// Snapshot of current engine state passed to each rule per evaluation.
/// Rules read from buffers and thresholds; they must not mutate anything.
/// </summary>
public sealed class CorrelationContext
{
    public required IReadOnlyDictionary<string, IReadOnlyList<Reading>> BufferSnapshots { get; init; }
    public required ThresholdConfig Thresholds { get; init; }
    public required DateTimeOffset Now { get; init; }
}

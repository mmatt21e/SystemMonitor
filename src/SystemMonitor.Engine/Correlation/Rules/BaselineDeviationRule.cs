using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Flags a reading that deviates from its rolling mean by more than N standard deviations.
/// Classified Indeterminate — the deviation is interesting but not enough to classify on its own;
/// the operator should review alongside other rules.
/// </summary>
public sealed class BaselineDeviationRule : ICorrelationRule
{
    private readonly string _source;
    private readonly string _metric;

    public BaselineDeviationRule(string sourceName, string metric)
    {
        _source = sourceName;
        _metric = metric;
    }

    public string Name => $"BaselineDeviation({_source}:{_metric})";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue(_source, out var buf)) yield break;
        var samples = buf.Where(r => r.Metric == _metric).OrderBy(r => r.Timestamp).ToList();
        if (samples.Count < 30) yield break;

        var latest = samples[^1];
        var window = samples.Take(samples.Count - 1).ToList();

        double mean = window.Average(r => r.Value);
        double variance = window.Sum(r => (r.Value - mean) * (r.Value - mean)) / window.Count;
        double stddev = Math.Sqrt(variance);
        if (stddev < 1e-6) yield break;

        double z = Math.Abs(latest.Value - mean) / stddev;
        if (z >= ctx.Thresholds.BaselineStdDevWarn)
        {
            yield return new AnomalyEvent(
                Timestamp: latest.Timestamp,
                Classification: Classification.Indeterminate,
                Confidence: Math.Min(0.6, z / 10),
                Summary: $"{_source}:{_metric} deviated {z:F1}σ from baseline (value={latest.Value:F2}, mean={mean:F2})",
                Explanation: $"Latest {_source}:{_metric} value of {latest.Value:F2} is {z:F1} standard deviations above the recent mean of {mean:F2}. Flagged for review — on its own this does not classify as Internal vs. External; look for correlated events.",
                SourceMetrics: new[] { $"{_source}:{_metric}" });
        }
    }
}

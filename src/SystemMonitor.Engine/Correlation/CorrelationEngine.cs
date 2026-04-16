using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using Timer = System.Threading.Timer;

namespace SystemMonitor.Engine.Correlation;

public sealed class CorrelationEngine : IDisposable
{
    private readonly IReadOnlyList<ICorrelationRule> _rules;
    private readonly IReadOnlyDictionary<string, ReadingRingBuffer> _buffers;
    private readonly ThresholdConfig _thresholds;
    private readonly Action<AnomalyEvent> _sink;
    private Timer? _timer;

    public CorrelationEngine(
        IEnumerable<ICorrelationRule> rules,
        IReadOnlyDictionary<string, ReadingRingBuffer> buffers,
        ThresholdConfig thresholds,
        Action<AnomalyEvent> sink)
    {
        _rules = rules.ToList();
        _buffers = buffers;
        _thresholds = thresholds;
        _sink = sink;
    }

    public void Start(TimeSpan interval) => _timer = new Timer(_ => EvaluateOnce(), null, interval, interval);

    public void Stop() => _timer?.Dispose();

    public void EvaluateOnce()
    {
        var ctx = new CorrelationContext
        {
            BufferSnapshots = _buffers.ToDictionary(kv => kv.Key, kv => kv.Value.Snapshot()),
            Thresholds = _thresholds,
            Now = DateTimeOffset.UtcNow
        };

        foreach (var rule in _rules)
        {
            IEnumerable<AnomalyEvent> produced;
            try { produced = rule.Evaluate(ctx); }
            catch { continue; }  // A broken rule must not crash the engine.

            foreach (var e in produced) _sink(e);
        }
    }

    public void Dispose() => Stop();
}

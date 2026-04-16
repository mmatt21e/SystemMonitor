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

    public void Stop()
    {
        var timer = _timer;
        if (timer is null) return;
        _timer = null;
        // 5s cap is a safety valve — callbacks are expected to drain in milliseconds.
        // Intentionally do NOT dispose the ManualResetEvent: the timer infrastructure may
        // still hold a reference to the underlying SafeWaitHandle briefly after signaling,
        // and disposing races with that. Let the GC reclaim it.
        var waitHandle = new ManualResetEvent(false);
        if (timer.Dispose(waitHandle))
        {
            waitHandle.WaitOne(TimeSpan.FromSeconds(5));
        }
        else
        {
            waitHandle.Dispose();
        }
    }

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

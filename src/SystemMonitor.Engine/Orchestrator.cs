using System.Collections.Concurrent;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using Timer = System.Threading.Timer;

namespace SystemMonitor.Engine;

/// <summary>
/// Drives each collector on its own timer. Each tick's readings are added to the
/// per-collector ring buffer AND published to the sink callback (typically the logger).
/// </summary>
public sealed class Orchestrator : IDisposable
{
    private readonly IReadOnlyList<ICollector> _collectors;
    private readonly IReadOnlyDictionary<string, ReadingRingBuffer> _buffers;
    private readonly Action<Reading> _sink;
    private readonly List<Timer> _timers = new();
    private volatile bool _running;

    public Orchestrator(
        IEnumerable<ICollector> collectors,
        IReadOnlyDictionary<string, ReadingRingBuffer> buffers,
        Action<Reading> sink)
    {
        _collectors = collectors.ToList();
        _buffers = buffers;
        _sink = sink;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        foreach (var c in _collectors)
        {
            // Interval == Zero means one-shot: fire once immediately, don't repeat.
            if (c.PollingInterval == TimeSpan.Zero)
            {
                Tick(c);
                continue;
            }
            var timer = new Timer(_ => Tick(c), null, TimeSpan.Zero, c.PollingInterval);
            _timers.Add(timer);
        }
    }

    public void Stop()
    {
        _running = false;
        var pending = new List<WaitHandle>();
        foreach (var t in _timers)
        {
            var waitHandle = new ManualResetEvent(false);
            if (t.Dispose(waitHandle))
                pending.Add(waitHandle);
            else
                waitHandle.Dispose();
        }
        if (pending.Count > 0)
        {
            // Per-handle WaitOne is used instead of WaitHandle.WaitAll because WaitAll on
            // multiple handles is NotSupported on STA threads — and WinForms' UI thread is STA,
            // so Stop() may be called from the WM_CLOSE handler on the STA thread.
            //
            // 5s per handle is a safety valve — callbacks are expected to drain in milliseconds.
            // Intentionally do NOT dispose the ManualResetEvents: the timer infrastructure may
            // still hold a reference to the underlying SafeWaitHandle briefly after signaling,
            // and disposing races with that. Let the GC reclaim them.
            foreach (var h in pending)
                h.WaitOne(TimeSpan.FromSeconds(5));
        }
        _timers.Clear();
    }

    private void Tick(ICollector c)
    {
        if (!_running) return;
        var readings = c.Collect();
        if (!_buffers.TryGetValue(c.Name, out var buf)) return;
        foreach (var r in readings)
        {
            buf.Add(r);
            _sink(r);
        }
    }

    public void Dispose() => Stop();
}

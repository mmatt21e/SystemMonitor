using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Buffer;

/// <summary>
/// Thread-safe fixed-capacity circular buffer of readings. When full, the oldest
/// reading is overwritten. Consumers (UI, correlation engine) read snapshots.
/// </summary>
public sealed class ReadingRingBuffer
{
    private readonly Reading[] _items;
    private readonly object _lock = new();
    private int _head;     // index of next write slot
    private int _count;

    public ReadingRingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new Reading[capacity];
    }

    public int Capacity => _items.Length;

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    public void Add(Reading reading)
    {
        lock (_lock)
        {
            _items[_head] = reading;
            _head = (_head + 1) % _items.Length;
            if (_count < _items.Length) _count++;
        }
    }

    /// <summary>Returns a chronological copy (oldest → newest).</summary>
    public IReadOnlyList<Reading> Snapshot()
    {
        lock (_lock)
        {
            var result = new Reading[_count];
            var start = _count < _items.Length ? 0 : _head;
            for (int i = 0; i < _count; i++)
                result[i] = _items[(start + i) % _items.Length];
            return result;
        }
    }
}

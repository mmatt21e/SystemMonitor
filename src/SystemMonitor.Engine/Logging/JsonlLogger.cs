using System.Text;
using System.Text.Json;
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Logging;

/// <summary>
/// Writes JSON-lines logs to disk. One file per day per category, rotating by size.
/// Write-ahead buffered; flush on every anomaly event and every 5 seconds otherwise
/// (caller controls flush cadence).
/// </summary>
public sealed class JsonlLogger : ILogger
{
    private readonly string _directory;
    private readonly string _category;
    private readonly long _rotationBytes;
    private readonly object _lock = new();
    private StreamWriter _writer;
    private string _currentPath;
    private long _currentBytes;

    public JsonlLogger(string directory, string category, long rotationBytes)
    {
        _directory = directory;
        _category = category;
        _rotationBytes = rotationBytes;
        _currentPath = LogRotator.NextFilePath(_directory, _category);
        _writer = new StreamWriter(_currentPath, append: false, Encoding.UTF8) { AutoFlush = false };
        _currentBytes = 0;
    }

    public string CurrentPath { get { lock (_lock) return _currentPath; } }

    public void WriteReading(Reading reading) => WriteLine(JsonSerializer.Serialize(reading));

    public void WriteLine(string jsonLine)
    {
        lock (_lock)
        {
            _writer.WriteLine(jsonLine);
            _currentBytes += Encoding.UTF8.GetByteCount(jsonLine) + Environment.NewLine.Length;
            if (_currentBytes >= _rotationBytes) Rotate();
        }
    }

    public void Flush()
    {
        lock (_lock) _writer.Flush();
    }

    private void Rotate()
    {
        _writer.Flush();
        _writer.Dispose();
        _currentPath = LogRotator.NextFilePath(_directory, _category);
        _writer = new StreamWriter(_currentPath, append: false, Encoding.UTF8) { AutoFlush = false };
        _currentBytes = 0;
    }

    public void Dispose()
    {
        lock (_lock) { _writer.Flush(); _writer.Dispose(); }
    }
}

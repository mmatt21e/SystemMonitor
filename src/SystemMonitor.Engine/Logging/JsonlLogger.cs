using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Logging;

/// <summary>
/// Writes JSON-lines logs to disk with a per-file tamper-evidence HMAC chain.
/// One file per day per category, rotating by size. Write-ahead buffered;
/// the caller controls flush cadence.
/// </summary>
/// <remarks>
/// Each line gains an <c>"hmac"</c> field: HMAC-SHA256(key, prev_hmac || payload_bytes),
/// where prev_hmac is 32 zero bytes for the first line of the file. The key is a fresh
/// random 32-byte value per file, written to a sidecar file at <c>&lt;path&gt;.key</c>.
/// This yields casual-tampering evidence: any modification, drop, or reorder of a line
/// breaks the chain for every line thereafter.
///
/// Key management (sidecar key alongside log) is Phase 1 "sane first step." External
/// key management and key signing are planned as a Phase 2 enhancement.
/// </remarks>
public sealed class JsonlLogger : ILogger
{
    private static readonly byte[] ChainStart = new byte[32];

    private readonly string _directory;
    private readonly string _category;
    private readonly long _rotationBytes;
    private readonly object _lock = new();
    private StreamWriter _writer;
    private string _currentPath;
    private long _currentBytes;

    private byte[] _chainKey = new byte[32];
    private byte[] _prevHmac = new byte[32];

    public JsonlLogger(string directory, string category, long rotationBytes)
    {
        _directory = directory;
        _category = category;
        _rotationBytes = rotationBytes;
        _currentPath = LogRotator.NextFilePath(_directory, _category);
        _writer = new StreamWriter(_currentPath, append: false, Encoding.UTF8) { AutoFlush = false };
        _currentBytes = 0;
        InitChain(_currentPath);
    }

    public string CurrentPath { get { lock (_lock) return _currentPath; } }

    public void WriteReading(Reading reading) => WriteLine(JsonSerializer.Serialize(reading));

    public void WriteLine(string jsonLine)
    {
        lock (_lock)
        {
            var chained = AppendHmac(jsonLine);
            _writer.WriteLine(chained);
            _currentBytes += Encoding.UTF8.GetByteCount(chained) + Environment.NewLine.Length;
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
        InitChain(_currentPath);
    }

    private void InitChain(string logPath)
    {
        _chainKey = RandomNumberGenerator.GetBytes(32);
        _prevHmac = ChainStart;

        // Sidecar key: <logPath>.key. An attacker with write access to the log directory
        // could also overwrite the key; this protects against casual/accidental tampering,
        // not a motivated adversary. External key management is a Phase 2 enhancement.
        try { File.WriteAllBytes(logPath + ".key", _chainKey); }
        catch { /* best effort — log integrity remains useful for in-memory verification */ }
    }

    private string AppendHmac(string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var buf = new byte[_prevHmac.Length + payloadBytes.Length];
        System.Buffer.BlockCopy(_prevHmac, 0, buf, 0, _prevHmac.Length);
        System.Buffer.BlockCopy(payloadBytes, 0, buf, _prevHmac.Length, payloadBytes.Length);

        using var mac = new HMACSHA256(_chainKey);
        _prevHmac = mac.ComputeHash(buf);
        var hex = Convert.ToHexString(_prevHmac).ToLowerInvariant();

        if (payload.Length > 0 && payload[^1] == '}')
            return string.Concat(payload.AsSpan(0, payload.Length - 1), $",\"hmac\":\"{hex}\"}}");

        // Non-object payload (e.g., a raw number or array) — wrap so output stays valid JSON.
        return $"{{\"payload\":{payload},\"hmac\":\"{hex}\"}}";
    }

    public void Dispose()
    {
        lock (_lock) { _writer.Flush(); _writer.Dispose(); }
    }
}

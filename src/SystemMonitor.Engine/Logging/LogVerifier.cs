using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SystemMonitor.Engine.Logging;

/// <summary>
/// Verifies the HMAC chain written by <see cref="JsonlLogger"/>. Reads the sidecar
/// key at <c>&lt;path&gt;.key</c>, recomputes each line's HMAC using the previous
/// line's HMAC as prefix, and reports the first mismatch.
/// </summary>
public sealed record LogVerificationResult(
    bool Ok,
    int LinesChecked,
    int? FirstFailureLine,
    long? FirstFailureByteOffset,
    string? Error);

public static class LogVerifier
{
    private static readonly byte[] ChainStart = new byte[32];
    private static readonly Regex HmacRe = new("\"hmac\":\"([0-9a-f]{64})\"", RegexOptions.Compiled);

    public static LogVerificationResult Verify(string logPath)
    {
        if (!File.Exists(logPath))
            return new LogVerificationResult(false, 0, null, null, $"Log file not found: {logPath}");

        var keyPath = logPath + ".key";
        if (!File.Exists(keyPath))
            return new LogVerificationResult(false, 0, null, null, $"Chain key sidecar not found: {keyPath}");

        byte[] key;
        try { key = File.ReadAllBytes(keyPath); }
        catch (Exception ex) { return new LogVerificationResult(false, 0, null, null, $"Could not read key: {ex.Message}"); }

        if (key.Length != 32)
            return new LogVerificationResult(false, 0, null, null, $"Chain key must be 32 bytes, got {key.Length}");

        var prevHmac = ChainStart;
        int lineNum = 0;
        long byteOffset = 0;

        foreach (var (line, offset) in ReadLinesWithOffsets(logPath))
        {
            lineNum++;
            var match = HmacRe.Match(line);
            if (!match.Success)
                return new LogVerificationResult(false, lineNum, lineNum, offset, $"Line {lineNum} has no hmac field");

            var storedHex = match.Groups[1].Value;
            var storedHmac = Convert.FromHexString(storedHex);

            // Reconstruct the payload: the line minus the HMAC insertion
            // (",\"hmac\":\"<hex>\"" placed immediately before the trailing '}').
            var payload = line.Substring(0, match.Index - 1) + "}";

            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var buf = new byte[prevHmac.Length + payloadBytes.Length];
            System.Buffer.BlockCopy(prevHmac, 0, buf, 0, prevHmac.Length);
            System.Buffer.BlockCopy(payloadBytes, 0, buf, prevHmac.Length, payloadBytes.Length);

            using var mac = new HMACSHA256(key);
            var computed = mac.ComputeHash(buf);

            if (!CryptographicOperations.FixedTimeEquals(computed, storedHmac))
                return new LogVerificationResult(false, lineNum, lineNum, offset,
                    $"HMAC mismatch at line {lineNum} (byte offset {offset})");

            prevHmac = computed;
            byteOffset = offset + Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        }

        return new LogVerificationResult(true, lineNum, null, null, null);
    }

    private static IEnumerable<(string Line, long ByteOffset)> ReadLinesWithOffsets(string path)
    {
        long offset = 0;
        foreach (var line in File.ReadLines(path))
        {
            yield return (line, offset);
            offset += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        }
    }
}

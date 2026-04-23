using System.Security.Cryptography;
using System.Text;

namespace SystemMonitor.Engine.Privacy;

/// <summary>
/// Redacts personally-identifiable fields (hostname, username, IP, MAC, serial, UUID) from
/// captured readings according to <see cref="PrivacyMode"/>. The salt is chosen per run so
/// the same physical machine gets different redacted values across runs — preventing cross-run
/// correlation by a third party holding only the logs.
/// </summary>
public sealed class PiiRedactor
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "hostname", "machine_name",
        "username", "user",
        "ip", "ip_address", "target_ip", "gateway_ip", "source_ip",
        "mac", "mac_address",
        "serial", "serial_number", "serialnumber",
        "uuid"
    };

    private readonly byte[] _salt;

    public PrivacyMode Mode { get; }
    public string SaltFingerprint { get; }

    public PiiRedactor(PrivacyMode mode, byte[]? salt = null)
    {
        Mode = mode;
        _salt = salt is { Length: > 0 } ? (byte[])salt.Clone() : RandomNumberGenerator.GetBytes(32);

        // Fingerprint = HMAC(salt, "fingerprint") truncated — lets us record which salt was in
        // use without exposing the salt itself. Analysts comparing runs can match by fingerprint.
        using var mac = new HMACSHA256(_salt);
        var bytes = mac.ComputeHash(Encoding.UTF8.GetBytes("fingerprint"));
        SaltFingerprint = Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }

    public string RedactHostname(string value) => Redact(value, "h");
    public string RedactUsername(string value) => Redact(value, "u");
    public string RedactIp(string value) => Redact(value, "ip");
    public string RedactMac(string value) => Redact(value, "m");
    public string RedactSerial(string value) => Redact(value, "s");

    public IReadOnlyDictionary<string, string> RedactLabels(IReadOnlyDictionary<string, string> labels)
    {
        if (Mode == PrivacyMode.Full) return labels;

        var result = new Dictionary<string, string>(labels.Count);
        foreach (var kv in labels)
        {
            result[kv.Key] = SensitiveKeys.Contains(kv.Key) ? RedactGeneric(kv.Value) : kv.Value;
        }
        return result;
    }

    private string Redact(string value, string prefix)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Mode switch
        {
            PrivacyMode.Full => value,
            PrivacyMode.Anonymous => "<redacted>",
            PrivacyMode.Redacted => $"{prefix}:{Hash(value)}",
            _ => value
        };
    }

    private string RedactGeneric(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Mode switch
        {
            PrivacyMode.Full => value,
            PrivacyMode.Anonymous => "<redacted>",
            PrivacyMode.Redacted => Hash(value),
            _ => value
        };
    }

    private string Hash(string value)
    {
        using var mac = new HMACSHA256(_salt);
        var bytes = mac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }
}

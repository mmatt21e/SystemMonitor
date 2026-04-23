using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Privacy;

/// <summary>
/// Applies <see cref="PiiRedactor"/> to a <see cref="Reading"/> — both its label dictionary
/// (via key-name heuristics) and any metric whose "value" label is known to carry PII
/// (e.g. inventory/machine_name).
/// </summary>
public sealed class ReadingSanitizer
{
    private static readonly HashSet<(string Source, string Metric)> SensitiveMetrics = new()
    {
        ("inventory", "machine_name")
    };

    private readonly PiiRedactor _redactor;

    public ReadingSanitizer(PiiRedactor redactor) => _redactor = redactor;

    public Reading Sanitize(Reading reading)
    {
        if (_redactor.Mode == PrivacyMode.Full) return reading;

        var labels = _redactor.RedactLabels(reading.Labels);

        if (SensitiveMetrics.Contains((reading.Source, reading.Metric))
            && labels.TryGetValue("value", out var rawValue))
        {
            var replaced = new Dictionary<string, string>(labels)
            {
                ["value"] = _redactor.RedactHostname(rawValue)
            };
            labels = replaced;
        }

        return reading with { Labels = labels };
    }
}

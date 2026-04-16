namespace SystemMonitor.Engine.Correlation;

public sealed record AnomalyEvent(
    DateTimeOffset Timestamp,
    Classification Classification,
    double Confidence,               // 0.0 – 1.0
    string Summary,                  // one-line human summary
    string Explanation,              // longer paragraph — what + why
    IReadOnlyList<string> SourceMetrics);  // "source:metric" identifiers that triggered this

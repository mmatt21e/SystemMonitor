namespace SystemMonitor.Engine.Correlation;

public interface ICorrelationRule
{
    string Name { get; }

    /// <summary>Returns any anomalies this rule detects in the given context, or empty.</summary>
    IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx);
}

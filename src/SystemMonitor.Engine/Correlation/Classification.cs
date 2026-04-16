namespace SystemMonitor.Engine.Correlation;

public enum Classification
{
    Internal,       // hardware originating in this PC
    External,       // environmental (power, thermal, network)
    Indeterminate   // insufficient data to classify
}

using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Logging;

public interface ILogger : IDisposable
{
    void WriteReading(Reading reading);
    void WriteLine(string jsonLine);     // for non-reading payloads (events, anomalies, headers)
    void Flush();
}

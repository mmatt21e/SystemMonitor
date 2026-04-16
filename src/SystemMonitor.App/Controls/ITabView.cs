using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public interface ITabView
{
    void Refresh(EngineHost host);
}

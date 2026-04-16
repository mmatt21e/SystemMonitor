using System.Windows.Forms;
using SystemMonitor.Engine;

namespace SystemMonitor.App.ViewModels;

/// <summary>
/// Ticks on the UI thread at the configured refresh rate. Subscribers receive the
/// latest buffer snapshots and render without having to subscribe to each reading event.
/// </summary>
public sealed class UiRefreshPump : IDisposable
{
    private readonly EngineHost _host;
    private readonly System.Windows.Forms.Timer _timer;

    public event Action<EngineHost>? Tick;

    public UiRefreshPump(EngineHost host, int refreshHz, Control uiThreadOwner)
    {
        _ = uiThreadOwner;
        _host = host;
        _timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(1, 1000 / Math.Max(1, refreshHz))
        };
        _timer.Tick += (_, _) => Tick?.Invoke(_host);
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}

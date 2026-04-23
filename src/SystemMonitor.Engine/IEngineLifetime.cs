namespace SystemMonitor.Engine;

/// <summary>
/// Lifecycle surface of a running engine. Exposed so hosting layers (Windows
/// Service worker, WinForms UI, headless console) can manage the engine
/// without a hard dependency on <see cref="EngineHost"/> -- and so tests can
/// substitute a fake.
/// </summary>
public interface IEngineLifetime : IDisposable
{
    void Start();
    void Stop();
}

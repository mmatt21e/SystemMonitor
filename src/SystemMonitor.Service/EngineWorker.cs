using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SystemMonitor.Engine;

namespace SystemMonitor.Service;

/// <summary>
/// Hosts an <see cref="IEngineLifetime"/> as a long-running <see cref="BackgroundService"/>.
/// The engine is built lazily in <see cref="ExecuteAsync"/> so construction failures
/// (missing config, privilege issues, hardware init) surface through the hosting
/// infrastructure's logging rather than crashing the process before the service
/// controller sees the start response.
/// </summary>
public sealed class EngineWorker : BackgroundService
{
    private readonly Func<IEngineLifetime> _factory;
    private readonly ILogger<EngineWorker> _logger;
    private IEngineLifetime? _engine;

    public EngineWorker(Func<IEngineLifetime> factory, ILogger<EngineWorker> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stopToken)
    {
        _logger.LogInformation("SystemMonitor engine starting.");
        _engine = _factory();
        _engine.Start();
        _logger.LogInformation("SystemMonitor engine started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stopToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SystemMonitor engine stopping.");
        try
        {
            _engine?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Engine.Stop() threw; continuing shutdown.");
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        try { _engine?.Dispose(); }
        catch (Exception ex) { _logger.LogError(ex, "Engine.Dispose() threw."); }
        base.Dispose();
    }
}

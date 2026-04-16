using FluentAssertions;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests;

[Collection("Lhm")]
public class EngineHostSmokeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public EngineHostSmokeTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public async Task Start_ProducesReadingsAcrossMultipleCollectors()
    {
        var cfg = AppConfig.Defaults();
        cfg.LogOutputDirectory = _dir;
        foreach (var c in cfg.Collectors.Values) c.PollingIntervalMs = Math.Min(c.PollingIntervalMs, 500);

        using var host = EngineHost.Build(cfg);
        host.Start();
        await Task.Delay(1500);
        host.Stop();

        host.Buffers.Should().ContainKey("cpu");
        host.Buffers["cpu"].Count.Should().BeGreaterThan(0);
        host.Buffers["memory"].Count.Should().BeGreaterThan(0);
    }
}

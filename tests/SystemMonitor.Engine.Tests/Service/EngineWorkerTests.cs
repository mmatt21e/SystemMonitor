using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SystemMonitor.Engine;
using SystemMonitor.Service;
using Xunit;

namespace SystemMonitor.Engine.Tests.Service;

public class EngineWorkerTests
{
    private sealed class FakeLifetime : IEngineLifetime
    {
        public int StartCount;
        public int StopCount;
        public int DisposeCount;

        public void Start() => StartCount++;
        public void Stop() => StopCount++;
        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public async Task StartAsync_CallsStartOnLifetime()
    {
        var fake = new FakeLifetime();
        using var worker = new EngineWorker(() => fake, NullLogger<EngineWorker>.Instance);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        fake.StartCount.Should().Be(1);
        fake.StopCount.Should().Be(0);

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CallsStopOnLifetime_DisposeHappensOnWorkerDispose()
    {
        var fake = new FakeLifetime();
        var worker = new EngineWorker(() => fake, NullLogger<EngineWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Hosting contract: StopAsync signals shutdown but doesn't dispose.
        fake.StopCount.Should().Be(1);
        fake.DisposeCount.Should().Be(0);

        // Dispose is the host's responsibility — when it happens, the engine is disposed too.
        worker.Dispose();

        fake.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOnCancellation()
    {
        var fake = new FakeLifetime();
        using var worker = new EngineWorker(() => fake, NullLogger<EngineWorker>.Instance);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        cts.Cancel();

        // Worker should stop cleanly within a short grace period.
        var stopped = worker.StopAsync(CancellationToken.None);
        var completed = await Task.WhenAny(stopped, Task.Delay(TimeSpan.FromSeconds(3)));

        completed.Should().Be(stopped);
    }

    [Fact]
    public async Task StartAsync_FactoryThrowing_BubblesUpBeforeExecution()
    {
        using var worker = new EngineWorker(
            () => throw new InvalidOperationException("engine init failed"),
            NullLogger<EngineWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var act = async () =>
        {
            await worker.StartAsync(cts.Token);
            // Give ExecuteAsync a tick to run and surface the factory exception.
            await Task.Delay(50);
            cts.Cancel();
            await worker.StopAsync(CancellationToken.None);
        };

        // Either StartAsync or StopAsync will surface the InvalidOperationException;
        // what matters is that it is not swallowed silently.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("engine init failed"));
    }
}

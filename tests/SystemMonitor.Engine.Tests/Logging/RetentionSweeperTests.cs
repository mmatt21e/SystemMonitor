using FluentAssertions;
using SystemMonitor.Engine.Logging;
using Xunit;

namespace SystemMonitor.Engine.Tests.Logging;

public class RetentionSweeperTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public RetentionSweeperTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private string Touch(string name, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    [Fact]
    public void Sweep_WithZeroDays_DeletesNothing()
    {
        var old = Touch("readings-2020-01-01.jsonl", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        RetentionSweeper.Sweep(_dir, retentionDays: 0);

        File.Exists(old).Should().BeTrue();
    }

    [Fact]
    public void Sweep_DeletesFilesOlderThanCutoff()
    {
        var now = DateTime.UtcNow;
        var old = Touch("readings-2020-01-01.jsonl", now.AddDays(-30));
        var recent = Touch("readings-2026-04-01.jsonl", now.AddDays(-2));

        RetentionSweeper.Sweep(_dir, retentionDays: 7);

        File.Exists(old).Should().BeFalse();
        File.Exists(recent).Should().BeTrue();
    }

    [Fact]
    public void Sweep_OnlyTouchesKnownCategories()
    {
        var now = DateTime.UtcNow;
        var oldReadings = Touch("readings-2020-01-01.jsonl", now.AddDays(-30));
        var oldEvents = Touch("events-2020-01-01.jsonl", now.AddDays(-30));
        var oldAnomalies = Touch("anomalies-2020-01-01.jsonl", now.AddDays(-30));
        var oldDiagnostics = Touch("diagnostics.log", now.AddDays(-30));
        var oldUnrelated = Touch("config.json", now.AddDays(-30));
        var oldInventory = Touch("inventory.json", now.AddDays(-30));

        RetentionSweeper.Sweep(_dir, retentionDays: 7);

        File.Exists(oldReadings).Should().BeFalse();
        File.Exists(oldEvents).Should().BeFalse();
        File.Exists(oldAnomalies).Should().BeFalse();

        // Diagnostics, config, and inventory snapshot are not time-series logs — leave them alone.
        File.Exists(oldDiagnostics).Should().BeTrue();
        File.Exists(oldUnrelated).Should().BeTrue();
        File.Exists(oldInventory).Should().BeTrue();
    }

    [Fact]
    public void Sweep_MissingDirectoryIsNoOp()
    {
        var missing = Path.Combine(_dir, "does-not-exist");

        var act = () => RetentionSweeper.Sweep(missing, retentionDays: 7);

        act.Should().NotThrow();
    }
}

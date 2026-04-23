using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class MinidumpAndPowerRuleTests
{
    private static Reading Minidump(DateTimeOffset ts, string? bugcheckCode = "0x00000139", string filename = "040123-12345-01.dmp")
    {
        var labels = new Dictionary<string, string>
        {
            ["filename"] = filename,
            ["path"] = $@"C:\Windows\Minidump\{filename}"
        };
        if (bugcheckCode is not null)
        {
            labels["bugcheck_code"] = bugcheckCode;
            labels["bugcheck_name"] = "KERNEL_SECURITY_CHECK_FAILURE";
        }
        return new Reading("reliability", "minidump", 262144, "bytes", ts,
            ReadingConfidence.High, labels);
    }

    private static Reading KernelPower41(DateTimeOffset ts) =>
        new("eventlog", "event", 2, "level", ts, ReadingConfidence.High,
            new Dictionary<string, string>
            {
                ["channel"] = "System",
                ["event_id"] = "41",
                ["level"] = "Error",
                ["provider"] = "Microsoft-Windows-Kernel-Power"
            });

    private static CorrelationContext Ctx(
        IReadOnlyList<Reading>? reliability = null,
        IReadOnlyList<Reading>? eventlog = null)
    {
        var snaps = new Dictionary<string, IReadOnlyList<Reading>>();
        if (reliability is not null) snaps["reliability"] = reliability;
        if (eventlog is not null) snaps["eventlog"] = eventlog;
        return new CorrelationContext
        {
            BufferSnapshots = snaps,
            Thresholds = new ThresholdConfig(),
            Now = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void MinidumpWithKernelPower41_Within60s_ClassifiedExternalWithExpectedSummary()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: new[] { Minidump(t) },
            eventlog: new[] { KernelPower41(t.AddSeconds(30)) });

        var events = new MinidumpAndPowerRule().Evaluate(ctx).ToList();

        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.External);
        events[0].Confidence.Should().BeGreaterThanOrEqualTo(0.8);
        events[0].Summary.Should().Be(
            "Kernel-Power 41 within 60s of BugCheck 0x00000139 — likely external power event, not a driver fault");
        events[0].SourceMetrics.Should().Contain("reliability:minidump");
        events[0].SourceMetrics.Should().Contain("eventlog:event(41)");
    }

    [Fact]
    public void MinidumpWithKernelPower41_ExactlyAt60s_IsConsideredWithinWindow()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: new[] { Minidump(t) },
            eventlog: new[] { KernelPower41(t.AddSeconds(60)) });

        new MinidumpAndPowerRule().Evaluate(ctx).Should().ContainSingle();
    }

    [Fact]
    public void MinidumpWithKernelPower41_Beyond60s_EmitsNothing()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: new[] { Minidump(t) },
            eventlog: new[] { KernelPower41(t.AddSeconds(61)) });

        new MinidumpAndPowerRule().Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void KernelPower41BeforeMinidump_WithinWindow_AlsoMatches()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: new[] { Minidump(t) },
            eventlog: new[] { KernelPower41(t.AddSeconds(-20)) });

        new MinidumpAndPowerRule().Evaluate(ctx).Should().ContainSingle()
            .Which.Classification.Should().Be(Classification.External);
    }

    [Fact]
    public void MinidumpAlone_WithoutKernelPower41_EmitsNothing()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: new[] { Minidump(t) },
            eventlog: Array.Empty<Reading>());

        new MinidumpAndPowerRule().Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void KernelPower41Alone_WithoutMinidump_EmitsNothing()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: Array.Empty<Reading>(),
            eventlog: new[] { KernelPower41(t) });

        new MinidumpAndPowerRule().Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void NoBuffersSnapshotted_EmitsNothing()
    {
        var ctx = Ctx();
        new MinidumpAndPowerRule().Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void MultipleMinidumps_OnlyThoseWithinWindow_Emit()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: new[]
            {
                Minidump(t.AddHours(-5), filename: "old.dmp"),      // far before
                Minidump(t.AddSeconds(-10), filename: "match.dmp"), // within window
                Minidump(t.AddHours(+5), filename: "future.dmp")    // far after
            },
            eventlog: new[] { KernelPower41(t) });

        var events = new MinidumpAndPowerRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Explanation.Should().Contain("match.dmp");
    }

    [Fact]
    public void MinidumpWithoutBugCheckCode_EmitsWithUnknownInSummary()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = Ctx(
            reliability: new[] { Minidump(t, bugcheckCode: null) },
            eventlog: new[] { KernelPower41(t.AddSeconds(5)) });

        var events = new MinidumpAndPowerRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Summary.Should().Contain("BugCheck unknown");
    }

    [Fact]
    public void ReliabilityReadingsOtherThanMinidump_AreIgnored()
    {
        var t = DateTimeOffset.UtcNow;
        var otherReliability = new Reading(
            "reliability", "record", 1, "count", t, ReadingConfidence.High,
            new Dictionary<string, string> { ["source"] = "Application Error", ["event_id"] = "1000" });
        var ctx = Ctx(
            reliability: new[] { otherReliability },
            eventlog: new[] { KernelPower41(t) });

        new MinidumpAndPowerRule().Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void EventlogReadingsOtherThanKernelPower41_AreIgnored()
    {
        var t = DateTimeOffset.UtcNow;
        var otherEvent = new Reading(
            "eventlog", "event", 3, "level", t, ReadingConfidence.High,
            new Dictionary<string, string>
            {
                ["channel"] = "System",
                ["event_id"] = "42",
                ["level"] = "Warning",
                ["provider"] = "Microsoft-Windows-Kernel-Power"
            });
        var ctx = Ctx(
            reliability: new[] { Minidump(t) },
            eventlog: new[] { otherEvent });

        new MinidumpAndPowerRule().Evaluate(ctx).Should().BeEmpty();
    }
}

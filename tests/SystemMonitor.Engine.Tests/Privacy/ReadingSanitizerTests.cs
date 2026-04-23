using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Privacy;
using Xunit;

namespace SystemMonitor.Engine.Tests.Privacy;

public class ReadingSanitizerTests
{
    private static readonly byte[] Salt = new byte[32];  // fixed zeros so outputs are stable

    private static Reading Make(string source, string metric, IReadOnlyDictionary<string, string> labels) =>
        new(source, metric, 1.0, "info", DateTimeOffset.UtcNow, ReadingConfidence.High, labels);

    [Fact]
    public void FullMode_PassesReadingsThroughUnchanged()
    {
        var sanitizer = new ReadingSanitizer(new PiiRedactor(PrivacyMode.Full, Salt));

        var r = Make("network", "link_up", new Dictionary<string, string>
        {
            ["adapter"] = "Ethernet",
            ["target_ip"] = "192.168.1.1"
        });

        var sanitized = sanitizer.Sanitize(r);

        sanitized.Labels["target_ip"].Should().Be("192.168.1.1");
        sanitized.Labels["adapter"].Should().Be("Ethernet");
    }

    [Fact]
    public void AnonymousMode_RedactsSensitiveLabels()
    {
        var sanitizer = new ReadingSanitizer(new PiiRedactor(PrivacyMode.Anonymous, Salt));

        var r = Make("network", "link_up", new Dictionary<string, string>
        {
            ["adapter"] = "Ethernet",
            ["target_ip"] = "192.168.1.1"
        });

        var sanitized = sanitizer.Sanitize(r);

        sanitized.Labels["target_ip"].Should().Be("<redacted>");
        sanitized.Labels["adapter"].Should().Be("Ethernet");
    }

    [Fact]
    public void MachineNameInventoryMetric_IsRedactedAcrossModes()
    {
        var anon = new ReadingSanitizer(new PiiRedactor(PrivacyMode.Anonymous, Salt));
        var red = new ReadingSanitizer(new PiiRedactor(PrivacyMode.Redacted, Salt));

        var r = Make("inventory", "machine_name", new Dictionary<string, string>
        {
            ["value"] = "DESKTOP-ABC123"
        });

        anon.Sanitize(r).Labels["value"].Should().Be("<redacted>");
        red.Sanitize(r).Labels["value"].Should()
            .NotBe("DESKTOP-ABC123")
            .And.StartWith("h:");
    }

    [Fact]
    public void NonSensitiveInventoryMetric_ValueLabelIsUntouched()
    {
        var sanitizer = new ReadingSanitizer(new PiiRedactor(PrivacyMode.Anonymous, Salt));

        var r = Make("inventory", "processor_count", new Dictionary<string, string>
        {
            ["value"] = "16"
        });

        sanitizer.Sanitize(r).Labels["value"].Should().Be("16");
    }

    [Fact]
    public void SanitizerRedactsSerialNumberKeys()
    {
        var sanitizer = new ReadingSanitizer(new PiiRedactor(PrivacyMode.Anonymous, Salt));

        var r = Make("inventory", "disk_info", new Dictionary<string, string>
        {
            ["Model"] = "Samsung SSD 980 PRO",
            ["SerialNumber"] = "S5GXNX0NA00123"
        });

        var sanitized = sanitizer.Sanitize(r);

        sanitized.Labels["SerialNumber"].Should().Be("<redacted>");
        sanitized.Labels["Model"].Should().Be("Samsung SSD 980 PRO");
    }
}

using FluentAssertions;
using SystemMonitor.Engine.Privacy;
using Xunit;

namespace SystemMonitor.Engine.Tests.Privacy;

public class PiiRedactorTests
{
    private static readonly byte[] Salt = new byte[]
    {
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
        0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10,
        0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
        0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20
    };

    [Fact]
    public void FullMode_ReturnsValuesUnchanged()
    {
        var r = new PiiRedactor(PrivacyMode.Full, Salt);

        r.RedactHostname("DESKTOP-ABC123").Should().Be("DESKTOP-ABC123");
        r.RedactUsername("matte").Should().Be("matte");
        r.RedactIp("192.168.1.10").Should().Be("192.168.1.10");
        r.RedactMac("AA-BB-CC-DD-EE-FF").Should().Be("AA-BB-CC-DD-EE-FF");
        r.RedactSerial("SN-12345678").Should().Be("SN-12345678");
    }

    [Fact]
    public void AnonymousMode_ReturnsFixedPlaceholder()
    {
        var r = new PiiRedactor(PrivacyMode.Anonymous, Salt);

        r.RedactHostname("DESKTOP-ABC123").Should().Be("<redacted>");
        r.RedactUsername("matte").Should().Be("<redacted>");
        r.RedactIp("192.168.1.10").Should().Be("<redacted>");
        r.RedactMac("AA-BB-CC-DD-EE-FF").Should().Be("<redacted>");
        r.RedactSerial("SN-12345678").Should().Be("<redacted>");
    }

    [Fact]
    public void RedactedMode_ProducesStableHashForSameInput()
    {
        var r = new PiiRedactor(PrivacyMode.Redacted, Salt);

        var a = r.RedactHostname("DESKTOP-ABC123");
        var b = r.RedactHostname("DESKTOP-ABC123");

        a.Should().Be(b);
        a.Should().NotBe("DESKTOP-ABC123");
        a.Should().StartWith("h:");
    }

    [Fact]
    public void RedactedMode_DifferentInputsProduceDifferentHashes()
    {
        var r = new PiiRedactor(PrivacyMode.Redacted, Salt);

        r.RedactHostname("host-a").Should().NotBe(r.RedactHostname("host-b"));
    }

    [Fact]
    public void RedactedMode_DifferentSaltsProduceDifferentHashes()
    {
        var salt1 = new byte[32]; salt1[0] = 1;
        var salt2 = new byte[32]; salt2[0] = 2;

        var r1 = new PiiRedactor(PrivacyMode.Redacted, salt1);
        var r2 = new PiiRedactor(PrivacyMode.Redacted, salt2);

        r1.RedactHostname("same-host").Should().NotBe(r2.RedactHostname("same-host"));
    }

    [Fact]
    public void RedactedMode_EmptyValueReturnsEmpty()
    {
        var r = new PiiRedactor(PrivacyMode.Redacted, Salt);

        r.RedactHostname("").Should().Be("");
        r.RedactIp("").Should().Be("");
    }

    [Fact]
    public void RedactLabels_RedactsKnownSensitiveKeys()
    {
        var r = new PiiRedactor(PrivacyMode.Anonymous, Salt);

        var input = new Dictionary<string, string>
        {
            ["adapter"] = "Ethernet",
            ["target"] = "gateway",
            ["target_ip"] = "192.168.1.1",
            ["value"] = "DESKTOP-ABC",   // caller decides — for hostname labels we use specific keys
            ["hostname"] = "DESKTOP-ABC",
            ["machine_name"] = "DESKTOP-ABC",
            ["username"] = "matte",
            ["user"] = "matte",
            ["mac"] = "AA-BB-CC-DD-EE-FF",
            ["ip"] = "10.0.0.1",
            ["ip_address"] = "10.0.0.1",
            ["serial"] = "SN-1",
            ["serial_number"] = "SN-1",
            ["uuid"] = "00000000-0000-0000-0000-000000000000"
        };

        var redacted = r.RedactLabels(input);

        // Non-sensitive keys pass through unchanged.
        redacted["adapter"].Should().Be("Ethernet");
        redacted["target"].Should().Be("gateway");

        // Sensitive keys are redacted.
        redacted["target_ip"].Should().Be("<redacted>");
        redacted["hostname"].Should().Be("<redacted>");
        redacted["machine_name"].Should().Be("<redacted>");
        redacted["username"].Should().Be("<redacted>");
        redacted["user"].Should().Be("<redacted>");
        redacted["mac"].Should().Be("<redacted>");
        redacted["ip"].Should().Be("<redacted>");
        redacted["ip_address"].Should().Be("<redacted>");
        redacted["serial"].Should().Be("<redacted>");
        redacted["serial_number"].Should().Be("<redacted>");
        redacted["uuid"].Should().Be("<redacted>");
    }

    [Fact]
    public void RedactLabels_IsCaseInsensitiveOnKeys()
    {
        var r = new PiiRedactor(PrivacyMode.Anonymous, Salt);

        var input = new Dictionary<string, string>
        {
            ["Hostname"] = "DESKTOP-ABC",
            ["MAC_Address"] = "AA-BB-CC-DD-EE-FF"
        };

        var redacted = r.RedactLabels(input);

        redacted["Hostname"].Should().Be("<redacted>");
        redacted["MAC_Address"].Should().Be("<redacted>");
    }

    [Fact]
    public void SaltFingerprint_IsStableAndShort()
    {
        var r1 = new PiiRedactor(PrivacyMode.Redacted, Salt);
        var r2 = new PiiRedactor(PrivacyMode.Redacted, Salt);

        r1.SaltFingerprint.Should().Be(r2.SaltFingerprint);
        r1.SaltFingerprint.Length.Should().BeLessOrEqualTo(16);
        r1.SaltFingerprint.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void SaltFingerprint_DiffersBetweenSalts()
    {
        var salt1 = new byte[32]; salt1[0] = 1;
        var salt2 = new byte[32]; salt2[0] = 2;

        var r1 = new PiiRedactor(PrivacyMode.Redacted, salt1);
        var r2 = new PiiRedactor(PrivacyMode.Redacted, salt2);

        r1.SaltFingerprint.Should().NotBe(r2.SaltFingerprint);
    }

    [Fact]
    public void ParameterlessConstructor_GeneratesRandomSalt()
    {
        var r1 = new PiiRedactor(PrivacyMode.Redacted);
        var r2 = new PiiRedactor(PrivacyMode.Redacted);

        // Two randomly-seeded redactors should (with overwhelming probability) hash the same input differently.
        r1.RedactHostname("host").Should().NotBe(r2.RedactHostname("host"));
    }
}

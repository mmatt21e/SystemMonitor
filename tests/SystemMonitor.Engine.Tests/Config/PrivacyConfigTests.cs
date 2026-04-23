using FluentAssertions;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Privacy;
using Xunit;

namespace SystemMonitor.Engine.Tests.Config;

public class PrivacyConfigTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public PrivacyConfigTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    [Fact]
    public void Defaults_PrivacyModeIsFull_RetentionIsZero()
    {
        var c = AppConfig.Defaults();
        c.Privacy.Mode.Should().Be(PrivacyMode.Full);
        c.LogRetentionDays.Should().Be(0);
    }

    [Fact]
    public void LoadOrDefaults_ParsesPrivacyMode()
    {
        var path = Path.Combine(_tempDir, "cfg.json");
        File.WriteAllText(path, """
            {
              "Privacy": { "Mode": "Anonymous" },
              "LogRetentionDays": 14
            }
            """);

        var (cfg, source) = ConfigLoader.LoadOrDefaults(path);

        cfg.Privacy.Mode.Should().Be(PrivacyMode.Anonymous);
        cfg.LogRetentionDays.Should().Be(14);
        source.Should().Be(ConfigSource.UserFile);
    }

    [Fact]
    public void LoadOrDefaults_UnspecifiedPrivacyFieldsKeepDefaults()
    {
        var path = Path.Combine(_tempDir, "cfg.json");
        File.WriteAllText(path, """
            { "UiRefreshHz": 5 }
            """);

        var (cfg, _) = ConfigLoader.LoadOrDefaults(path);

        cfg.Privacy.Mode.Should().Be(PrivacyMode.Full);
        cfg.LogRetentionDays.Should().Be(0);
    }
}

using FluentAssertions;
using SystemMonitor.Engine.Config;
using Xunit;

namespace SystemMonitor.Engine.Tests.Config;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ConfigLoaderTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    [Fact]
    public void LoadOrDefaults_FileMissing_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "missing.json");
        var (cfg, source) = ConfigLoader.LoadOrDefaults(path);
        cfg.Collectors.Should().ContainKey("cpu");
        source.Should().Be(ConfigSource.BuiltInDefaults);
    }

    [Fact]
    public void LoadOrDefaults_ValidFile_MergesUserValuesOverDefaults()
    {
        var path = Path.Combine(_tempDir, "cfg.json");
        File.WriteAllText(path, """
            {
              "UiRefreshHz": 5,
              "Collectors": { "cpu": { "PollingIntervalMs": 500 } }
            }
            """);
        var (cfg, source) = ConfigLoader.LoadOrDefaults(path);
        cfg.UiRefreshHz.Should().Be(5);
        cfg.Collectors["cpu"].PollingIntervalMs.Should().Be(500);
        // Unspecified values keep defaults:
        cfg.Collectors.Should().ContainKey("memory");
        cfg.LogRotationSizeBytes.Should().Be(100 * 1024 * 1024);
        source.Should().Be(ConfigSource.UserFile);
    }

    [Fact]
    public void LoadOrDefaults_MalformedJson_Throws()
    {
        var path = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(path, "{ this is not valid json");
        var act = () => ConfigLoader.LoadOrDefaults(path);
        act.Should().Throw<ConfigLoadException>().WithMessage("*parse*");
    }
}

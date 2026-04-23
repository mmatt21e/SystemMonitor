using System.ComponentModel;
using System.Text.Json.Serialization;
using SystemMonitor.Engine.Privacy;

namespace SystemMonitor.Engine.Config;

public sealed class PrivacyConfig
{
    [Description("Controls how personally-identifiable fields are handled in log output. Full = unchanged; Redacted = stable per-run hashes; Anonymous = replaced with <redacted>.")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PrivacyMode Mode { get; set; } = PrivacyMode.Full;
}

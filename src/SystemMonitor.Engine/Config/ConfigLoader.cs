using System.Text.Json;

namespace SystemMonitor.Engine.Config;

public enum ConfigSource { BuiltInDefaults, UserFile }

public sealed class ConfigLoadException : Exception
{
    public ConfigLoadException(string message, Exception? inner = null) : base(message, inner) { }
}

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads <paramref name="path"/> if it exists, merging user values over defaults.
    /// If the file is missing, returns built-in defaults. Throws <see cref="ConfigLoadException"/>
    /// on malformed JSON — we fail fast so the operator doesn't silently monitor with the wrong config.
    /// </summary>
    public static (AppConfig Config, ConfigSource Source) LoadOrDefaults(string path)
    {
        var defaults = AppConfig.Defaults();
        if (!File.Exists(path)) return (defaults, ConfigSource.BuiltInDefaults);

        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception ex) { throw new ConfigLoadException($"Could not read config file '{path}': {ex.Message}", ex); }

        AppConfig? user;
        try { user = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions); }
        catch (JsonException ex) { throw new ConfigLoadException($"Failed to parse config '{path}' at line {ex.LineNumber}, col {ex.BytePositionInLine}: {ex.Message}", ex); }

        if (user is null) return (defaults, ConfigSource.UserFile);

        return (Merge(defaults, user), ConfigSource.UserFile);
    }

    // Simple merge: user values override defaults at the leaf level. Collector dictionary
    // is merged key-by-key (user entries add to / override defaults).
    private static AppConfig Merge(AppConfig defaults, AppConfig user)
    {
        if (!string.IsNullOrWhiteSpace(user.LogOutputDirectory)) defaults.LogOutputDirectory = user.LogOutputDirectory;
        if (user.LogRotationSizeBytes > 0) defaults.LogRotationSizeBytes = user.LogRotationSizeBytes;
        if (user.UiRefreshHz > 0) defaults.UiRefreshHz = user.UiRefreshHz;
        if (user.BufferCapacityPerCollector > 0) defaults.BufferCapacityPerCollector = user.BufferCapacityPerCollector;
        if (user.CorrelationIntervalMs > 0) defaults.CorrelationIntervalMs = user.CorrelationIntervalMs;
        if (user.WmiTimeoutMs > 0) defaults.WmiTimeoutMs = user.WmiTimeoutMs;
        if (user.LogRetentionDays > 0) defaults.LogRetentionDays = user.LogRetentionDays;

        foreach (var kv in user.Collectors)
            defaults.Collectors[kv.Key] = kv.Value;

        if (user.Thresholds is not null)
            defaults.Thresholds = user.Thresholds;

        if (user.Privacy is not null)
            defaults.Privacy = user.Privacy;

        return defaults;
    }
}

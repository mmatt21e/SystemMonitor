namespace SystemMonitor.Engine.Logging;

/// <summary>
/// Deletes time-series log files (readings/events/anomalies) older than a retention window.
/// Never touches diagnostics.log, inventory.json, config files, or anything else — those
/// are either active tool state or one-shot snapshots, not time-series data.
/// </summary>
public static class RetentionSweeper
{
    private static readonly string[] Categories = { "readings", "events", "anomalies" };

    /// <summary>
    /// Deletes matching files with LastWriteTimeUtc older than <paramref name="retentionDays"/>
    /// days before now. When <paramref name="retentionDays"/> is 0 or negative, nothing is deleted —
    /// retention is opt-in so forensic data is never silently lost.
    /// </summary>
    public static void Sweep(string directory, int retentionDays)
    {
        if (retentionDays <= 0) return;
        if (!Directory.Exists(directory)) return;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        foreach (var category in Categories)
        {
            foreach (var path in Directory.EnumerateFiles(directory, $"{category}-*.jsonl"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                        File.Delete(path);
                }
                catch
                {
                    // Best-effort — file may be locked by another process or gone between enumerate and delete.
                }
            }
        }
    }
}

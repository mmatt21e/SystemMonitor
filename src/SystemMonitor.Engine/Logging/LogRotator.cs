namespace SystemMonitor.Engine.Logging;

internal static class LogRotator
{
    public static string NextFilePath(string directory, string category)
    {
        Directory.CreateDirectory(directory);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        int seq = 0;
        while (true)
        {
            var name = seq == 0
                ? $"{category}-{date}.jsonl"
                : $"{category}-{date}.{seq}.jsonl";
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) return path;
            seq++;
        }
    }
}

using System.Text.Json;

namespace SystemMonitor.Engine.Diagnostics;

/// <summary>
/// CLI-facing dump analyzer. Given a single .dmp file or a directory of .dmp files,
/// runs <see cref="MinidumpReader"/> against each and writes newline-delimited JSON
/// describing the parse result. Designed for scripting (e.g. corpus inventory).
/// </summary>
/// <remarks>
/// Field naming mirrors the snake_case used by the engine's capability-report JSON
/// and the reliability collector's dump labels, so downstream tooling can join on
/// the same keys without a translation step.
/// </remarks>
public static class MinidumpAnalyzeCommand
{
    public static int Run(string target, TextWriter output)
    {
        var files = ResolveFiles(target);
        if (files.Count == 0) return 2;

        foreach (var file in files)
            output.WriteLine(JsonSerializer.Serialize(Analyze(file)));

        return 0;
    }

    private static IReadOnlyList<string> ResolveFiles(string target)
    {
        if (File.Exists(target)) return new[] { target };
        if (Directory.Exists(target))
            return Directory.GetFiles(target, "*.dmp", SearchOption.TopDirectoryOnly);
        return Array.Empty<string>();
    }

    private static object Analyze(string path)
    {
        var fi = new FileInfo(path);
        var info = MinidumpReader.TryRead(path);
        if (info is null)
        {
            return new
            {
                path,
                filename = fi.Name,
                size_bytes = fi.Length,
                created_utc = fi.CreationTimeUtc,
                parsed = false,
                reason = "not a valid 64-bit kernel dump — DUMP_HEADER64 signature mismatch, file too short, or user-mode minidump"
            };
        }

        return new
        {
            path,
            filename = fi.Name,
            size_bytes = fi.Length,
            created_utc = fi.CreationTimeUtc,
            parsed = true,
            bugcheck_code = $"0x{info.BugCheckCode:X8}",
            bugcheck_name = info.BugCheckName,
            bugcheck_parameters = new[]
            {
                $"0x{info.BugCheckParameter1:x16}",
                $"0x{info.BugCheckParameter2:x16}",
                $"0x{info.BugCheckParameter3:x16}",
                $"0x{info.BugCheckParameter4:x16}"
            }
        };
    }
}

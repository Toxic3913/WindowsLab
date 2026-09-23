using System.Diagnostics;

namespace WindowsLab.Core;

public enum ProcessGroup
{
    Windows,
    Microsoft,
    External
}

public sealed record LiveProcessRow(
    int Id,
    string Name,
    ProcessGroup Group,
    double CpuPercent,
    long WorkingSetBytes,
    long PrivateBytes,
    string? Path);

public sealed record ProcessGroupTotals(
    ProcessGroup Group,
    int Count,
    long WorkingSetBytes);

public sealed record ProcessSampleResult(
    DateTimeOffset CapturedUtc,
    IReadOnlyList<LiveProcessRow> TopByWorkingSet,
    IReadOnlyList<LiveProcessRow> TopByCpu,
    IReadOnlyList<ProcessGroupTotals> GroupTotals);

/// <summary>
/// Samples running processes for the live dashboard (D022). Read-only — no terminate.
/// Classification is path-based (no Authenticode every tick).
/// </summary>
public static class ProcessSampler
{
    private static readonly object Gate = new();
    private static Dictionary<int, TimeSpan> _prevCpu = new();
    private static DateTimeOffset _prevSampleUtc = DateTimeOffset.MinValue;

    private static readonly HashSet<string> KnownSystemNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass",
        "svchost", "fontdrvhost", "dwm", "winlogon", "Memory Compression"
    };

    public static ProcessSampleResult Sample(int topN = 25)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(topN, 1);

        var now = DateTimeOffset.UtcNow;
        double elapsedSeconds;
        Dictionary<int, TimeSpan> prev;
        lock (Gate)
        {
            elapsedSeconds = _prevSampleUtc == DateTimeOffset.MinValue
                ? 0
                : Math.Max(0.001, (now - _prevSampleUtc).TotalSeconds);
            prev = _prevCpu;
        }

        var rows = new List<LiveProcessRow>(256);
        var nextCpu = new Dictionary<int, TimeSpan>(256);

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                using (p)
                {
                    string? path = null;
                    try
                    {
                        path = p.MainModule?.FileName;
                    }
                    catch
                    {
                        // AccessDenied / exited
                    }

                    TimeSpan totalCpu;
                    try
                    {
                        totalCpu = p.TotalProcessorTime;
                    }
                    catch
                    {
                        continue;
                    }

                    nextCpu[p.Id] = totalCpu;
                    var cpuPct = 0d;
                    if (elapsedSeconds > 0 && prev.TryGetValue(p.Id, out var before))
                    {
                        var delta = (totalCpu - before).TotalSeconds;
                        cpuPct = 100d * delta / (Environment.ProcessorCount * elapsedSeconds);
                        cpuPct = Math.Clamp(cpuPct, 0, 100);
                    }

                    long ws;
                    long priv;
                    try
                    {
                        ws = p.WorkingSet64;
                        priv = p.PrivateMemorySize64;
                    }
                    catch
                    {
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(p.ProcessName) ? $"pid-{p.Id}" : p.ProcessName;
                    var group = Classify(p.Id, name, path);
                    rows.Add(new LiveProcessRow(p.Id, name, group, cpuPct, ws, priv, path));
                }
            }
            catch
            {
                // process exited mid-sample
            }
        }

        lock (Gate)
        {
            _prevCpu = nextCpu;
            _prevSampleUtc = now;
        }

        var topWs = rows
            .OrderByDescending(r => r.WorkingSetBytes)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .ToArray();
        var topCpu = rows
            .OrderByDescending(r => r.CpuPercent)
            .ThenByDescending(r => r.WorkingSetBytes)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .ToArray();

        var totals = Enum.GetValues<ProcessGroup>()
            .Select(g =>
            {
                var gRows = rows.Where(r => r.Group == g).ToArray();
                return new ProcessGroupTotals(g, gRows.Length, gRows.Sum(r => r.WorkingSetBytes));
            })
            .ToArray();

        return new ProcessSampleResult(now, topWs, topCpu, totals);
    }

    /// <summary>Path heuristics for unit tests and live sampling.</summary>
    public static ProcessGroup Classify(int processId, string processName, string? path)
    {
        if (processId is 0 or 4)
        {
            return ProcessGroup.Windows;
        }

        if (KnownSystemNames.Contains(processName))
        {
            return ProcessGroup.Windows;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return ProcessGroup.External;
        }

        var normalized = path.Replace('/', '\\');
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(winDir) &&
            normalized.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
        {
            return ProcessGroup.Windows;
        }

        // Fallback when WINDIR unavailable in tests — match \Windows\ segment
        if (normalized.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\WinSxS\", StringComparison.OrdinalIgnoreCase))
        {
            // Exclude user-profile "Windows" folders that aren't the OS tree when possible
            if (normalized.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(@"\Windows\SysWOW64\", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(@"\Windows\explorer.exe", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(@"\WinSxS\", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(@"C:\WINDOWS\", StringComparison.OrdinalIgnoreCase))
            {
                return ProcessGroup.Windows;
            }
        }

        if (normalized.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessGroup.Microsoft;
        }

        if (normalized.Contains(@"\Program Files\WindowsApps\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Program Files\Microsoft\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Program Files (x86)\Microsoft\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Program Files\Microsoft Office\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Program Files (x86)\Microsoft Office\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Microsoft VS Code\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Microsoft Visual Studio\", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessGroup.Microsoft;
        }

        // Program Files\Microsoft* folder name
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (IsUnderMicrosoftProgramFiles(normalized, pf) || IsUnderMicrosoftProgramFiles(normalized, pf86))
        {
            return ProcessGroup.Microsoft;
        }

        return ProcessGroup.External;
    }

    private static bool IsUnderMicrosoftProgramFiles(string path, string? programFilesRoot)
    {
        if (string.IsNullOrEmpty(programFilesRoot))
        {
            return false;
        }

        var prefix = programFilesRoot.TrimEnd('\\') + @"\Microsoft";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resets CPU delta state (tests).</summary>
    public static void ResetCpuHistoryForTests()
    {
        lock (Gate)
        {
            _prevCpu = new Dictionary<int, TimeSpan>();
            _prevSampleUtc = DateTimeOffset.MinValue;
        }
    }
}

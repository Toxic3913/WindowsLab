using System.Diagnostics;
using System.ServiceProcess;
using System.Text.Json;

namespace WindowsLab.Core;

public sealed record ExternalWorkloadDefinition(
    string Id,
    string Title,
    string Description,
    RiskLevel Risk,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> ServiceNames);

public sealed record ExternalWorkloadStatus(
    string Id,
    string Title,
    bool Present,
    bool Active,
    int ProcessCount,
    long WorkingSetBytes,
    IReadOnlyList<string> RunningProcesses,
    IReadOnlyList<string> RunningServices,
    string Summary);

public sealed record ExternalWorkloadStopResult(
    string Id,
    bool Ok,
    int ProcessesStopped,
    int ServicesStopped,
    IReadOnlyList<string> Messages);

/// <summary>
/// Curated third-party workloads (Steam, Riot, Overwolf, …). Detect + runtime stop only.
/// Never touches DiagTrack / SysMain / WSearch / Defender (D028).
/// </summary>
public static class ExternalWorkloadCatalog
{
    public static IReadOnlyList<ExternalWorkloadDefinition> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        var list = new List<ExternalWorkloadDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            list.AddRange(LoadFile(file));
        }

        return list.OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<ExternalWorkloadDefinition> LoadFile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return doc.RootElement.EnumerateArray().Select(ParseOne).ToArray();
        }

        return [ParseOne(doc.RootElement)];
    }

    private static ExternalWorkloadDefinition ParseOne(JsonElement el)
    {
        var id = el.GetProperty("id").GetString()
                 ?? throw new InvalidDataException("workload id required");
        if (!id.StartsWith("workload.", StringComparison.Ordinal))
        {
            throw new InvalidDataException("workload id must start with workload.");
        }

        return new ExternalWorkloadDefinition(
            id,
            el.GetProperty("title").GetString() ?? id,
            el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            ParseRisk(el.TryGetProperty("risk", out var r) ? r.GetString() : "LOW"),
            ReadNames(el, "processNames"),
            ReadNames(el, "serviceNames"));
    }

    private static string[] ReadNames(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return p.EnumerateArray()
            .Select(x => x.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static RiskLevel ParseRisk(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "MEDIUM" => RiskLevel.Medium,
        "HIGH" => RiskLevel.High,
        "CRITICAL" => RiskLevel.Critical,
        _ => RiskLevel.Low
    };
}

public static class ExternalWorkloadController
{
    private static readonly HashSet<string> HardBlockedServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "DiagTrack", "dmwappushservice", "SysMain", "WSearch",
        "WinDefend", "Sense", "WdNisSvc", "WdFilter", "WdBoot"
    };

    private static readonly HashSet<string> HardBlockedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "csrss", "smss", "wininit", "services", "lsass", "svchost",
        "winlogon", "fontdrvhost", "dwm", "explorer", "MsMpEng", "SecurityHealthService",
        "SearchHost", "SearchIndexer", "Memory Compression"
    };

    public static ExternalWorkloadStatus Detect(ExternalWorkloadDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);

        var procs = MatchProcesses(def.ProcessNames);
        var services = MatchRunningServices(def.ServiceNames);
        var ws = procs.Sum(p =>
        {
            try { return p.WorkingSet64; }
            catch { return 0L; }
        });
        var procNames = procs.Select(p => p.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToArray();
        var present = procs.Count > 0 || ServiceExistsAny(def.ServiceNames);
        var active = procs.Count > 0 || services.Count > 0;
        var summary = !present
            ? "no instalado / inactivo"
            : active
                ? $"{procs.Count} proc · {services.Count} svc · {ws / (1024d * 1024):0} MB"
                : "instalado, parado";

        return new ExternalWorkloadStatus(
            def.Id,
            def.Title,
            present,
            active,
            procs.Count,
            ws,
            procNames,
            services.ToArray(),
            summary);
    }

    public static ExternalWorkloadStopResult Stop(ExternalWorkloadDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        var messages = new List<string>();
        var procStopped = 0;
        var svcStopped = 0;

        foreach (var name in def.ServiceNames)
        {
            if (HardBlockedServices.Contains(name))
            {
                messages.Add("bloqueado: " + name);
                continue;
            }

            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
                    svcStopped++;
                    messages.Add("svc stop: " + name);
                }
            }
            catch (Exception ex)
            {
                messages.Add($"svc {name}: {ex.Message}");
            }
        }

        foreach (var p in MatchProcesses(def.ProcessNames))
        {
            if (HardBlockedProcesses.Contains(p.ProcessName))
            {
                messages.Add("proc bloqueado: " + p.ProcessName);
                continue;
            }

            try
            {
                var id = p.Id;
                var pname = p.ProcessName;
                if (!p.HasExited && p.CloseMainWindow())
                {
                    if (!p.WaitForExit(2500) && !p.HasExited)
                    {
                        p.Kill(entireProcessTree: true);
                    }
                }
                else if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                }

                procStopped++;
                messages.Add($"proc stop: {pname} ({id})");
            }
            catch (Exception ex)
            {
                messages.Add($"proc {p.ProcessName}: {ex.Message}");
            }
            finally
            {
                try { p.Dispose(); } catch { /* ignore */ }
            }
        }

        var ok = procStopped + svcStopped > 0;
        return new ExternalWorkloadStopResult(def.Id, ok, procStopped, svcStopped, messages);
    }

    private static List<Process> MatchProcesses(IReadOnlyList<string> names)
    {
        var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var list = new List<Process>();
        if (set.Count == 0)
        {
            return list;
        }

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (set.Contains(p.ProcessName))
                {
                    list.Add(p);
                }
                else
                {
                    p.Dispose();
                }
            }
            catch
            {
                try { p.Dispose(); } catch { /* ignore */ }
            }
        }

        return list;
    }

    private static List<string> MatchRunningServices(IReadOnlyList<string> names)
    {
        var running = new List<string>();
        foreach (var name in names)
        {
            if (HardBlockedServices.Contains(name))
            {
                continue;
            }

            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                {
                    running.Add(name);
                }
            }
            catch
            {
                // service missing
            }
        }

        return running;
    }

    private static bool ServiceExistsAny(IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            try
            {
                using var sc = new ServiceController(name);
                _ = sc.Status;
                return true;
            }
            catch
            {
                // missing
            }
        }

        return false;
    }
}

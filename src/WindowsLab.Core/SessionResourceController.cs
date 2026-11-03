using System.Diagnostics;
using System.ServiceProcess;

namespace WindowsLab.Core;

public sealed record SessionProcessInfo(
    int Id,
    string Name,
    long WorkingSetBytes,
    long PrivateBytes,
    string? Path,
    ProcessGroup Group,
    bool IsBlocked);

public sealed record SessionServiceInfo(
    string Name,
    string DisplayName,
    string Status,
    bool CanStop,
    bool IsBlocked);

public sealed record SessionStopResult(
    int ProcessesStopped,
    int ServicesStopped,
    int BlockedSkipped,
    IReadOnlyList<string> Messages);

/// <summary>
/// Operator-selected process/service stop for Games “sesión ligera” (D032).
/// Hard-blocks Defender / DiagTrack / SysMain / WSearch / critical OS processes.
/// Stops services (runtime), does not permanently disable Start type.
/// </summary>
public static class SessionResourceController
{
    public static IReadOnlyList<SessionProcessInfo> ListProcesses(int topN = 80)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(topN, 1);
        var rows = new List<SessionProcessInfo>(256);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                using (p)
                {
                    string? path = null;
                    try { path = p.MainModule?.FileName; }
                    catch { /* AccessDenied */ }

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

                    var name = p.ProcessName;
                    rows.Add(new SessionProcessInfo(
                        p.Id,
                        name,
                        ws,
                        priv,
                        path,
                        ProcessSampler.Classify(p.Id, name, path),
                        ExternalWorkloadController.IsProcessHardBlocked(name)));
                }
            }
            catch
            {
                try { p.Dispose(); } catch { /* ignore */ }
            }
        }

        return rows
            .OrderByDescending(r => r.WorkingSetBytes)
            .Take(topN)
            .ToArray();
    }

    public static IReadOnlyList<SessionServiceInfo> ListRunningServices(int topN = 80)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(topN, 1);
        var list = new List<SessionServiceInfo>();
        ServiceController[] all;
        try
        {
            all = ServiceController.GetServices();
        }
        catch
        {
            return [];
        }

        foreach (var sc in all)
        {
            try
            {
                using (sc)
                {
                    if (sc.Status is not (ServiceControllerStatus.Running or ServiceControllerStatus.StartPending))
                    {
                        continue;
                    }

                    var blocked = ExternalWorkloadController.IsServiceHardBlocked(sc.ServiceName);
                    list.Add(new SessionServiceInfo(
                        sc.ServiceName,
                        string.IsNullOrWhiteSpace(sc.DisplayName) ? sc.ServiceName : sc.DisplayName,
                        sc.Status.ToString(),
                        !blocked && sc.CanStop,
                        blocked));
                }
            }
            catch
            {
                try { sc.Dispose(); } catch { /* ignore */ }
            }
        }

        return list
            .OrderBy(s => s.IsBlocked ? 1 : 0)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .ToArray();
    }

    public static SessionStopResult StopSelected(
        IEnumerable<int> processIds,
        IEnumerable<string> serviceNames)
    {
        var messages = new List<string>();
        var procStopped = 0;
        var svcStopped = 0;
        var blocked = 0;
        var idSet = new HashSet<int>(processIds);

        foreach (var name in serviceNames)
        {
            if (ExternalWorkloadController.IsServiceHardBlocked(name))
            {
                blocked++;
                messages.Add("svc bloqueado: " + name);
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

        foreach (var id in idSet)
        {
            Process? p = null;
            try
            {
                p = Process.GetProcessById(id);
                if (ExternalWorkloadController.IsProcessHardBlocked(p.ProcessName))
                {
                    blocked++;
                    messages.Add("proc bloqueado: " + p.ProcessName);
                    continue;
                }

                // Never Kill(entireProcessTree): could hit hard-blocked children (Defender, etc.).
                var pname = p.ProcessName;
                if (!p.HasExited && p.CloseMainWindow())
                {
                    if (!p.WaitForExit(2500) && !p.HasExited)
                    {
                        p.Kill(entireProcessTree: false);
                    }
                }
                else if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: false);
                }

                procStopped++;
                messages.Add($"proc stop: {pname} ({id})");
            }
            catch (Exception ex)
            {
                messages.Add($"proc {id}: {ex.Message}");
            }
            finally
            {
                try { p?.Dispose(); } catch { /* ignore */ }
            }
        }

        return new SessionStopResult(procStopped, svcStopped, blocked, messages);
    }
}

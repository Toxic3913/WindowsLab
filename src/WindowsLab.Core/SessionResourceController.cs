using System.Diagnostics;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace WindowsLab.Core;

public sealed record SessionProcessInfo(
    int Id,
    string Name,
    long WorkingSetBytes,
    long PrivateBytes,
    string? Path,
    string? WindowTitle,
    string? DisplayName,
    ProcessGroup Group,
    bool IsBlocked,
    bool IsBackgroundInterest);

public sealed record SessionServiceInfo(
    string Name,
    string DisplayName,
    string Status,
    bool IsRunning,
    bool CanStop,
    bool CanStart,
    bool IsBlocked,
    bool IsBackgroundInterest);

public sealed record SessionStopResult(
    int ProcessesStopped,
    int ServicesStopped,
    int BlockedSkipped,
    IReadOnlyList<string> Messages);

public sealed record SessionStartResult(
    int ServicesStarted,
    int BlockedSkipped,
    IReadOnlyList<string> Messages);

/// <summary>
/// Operator-selected process/service control for Cargas (D032).
/// Prefers background / third-party apps (VPN, Steam helpers, Ollama…) over a pure RAM top-N cut.
/// Hard-blocks Defender / DiagTrack / SysMain / WSearch / critical OS processes.
/// </summary>
public static partial class SessionResourceController
{
    private static readonly string[] BackgroundTokens =
    [
        "nord", "vpn", "forti", "mullvad", "expressvpn", "proton", "warp",
        "ollama", "docker", "steam", "lua", "epic", "ubisoft", "origin", "eadesktop",
        "battle.net", "agent", "discord", "overwolf", "riot", "vanguard", "faceit",
        "nvidia", "radeon", "lghub", "icue", "razor", "steelseries", "obs", "streamlabs",
        "dropbox", "onedrive", "googledrive", "mega", "spotify", "adobe", "creative",
        "teams", "slack", "zoom", "webex", "anydesk", "teamviewer", "parsec",
        "vmware", "virtualbox", "vbox", "wsl", "hyper-v"
    ];

    public static IReadOnlyList<SessionProcessInfo> ListProcesses(int maxRows = 500)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);
        var rows = new List<SessionProcessInfo>(512);
        var displayCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                using (p)
                {
                    string? path = null;
                    try { path = p.MainModule?.FileName; }
                    catch { /* AccessDenied */ }

                    string? title = null;
                    try
                    {
                        title = string.IsNullOrWhiteSpace(p.MainWindowTitle) ? null : p.MainWindowTitle.Trim();
                    }
                    catch
                    {
                        // ignore
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
                    var group = ProcessSampler.Classify(p.Id, name, path);
                    var blocked = ExternalWorkloadController.IsProcessHardBlocked(name);
                    // AccessDenied paths often mis-classify protected OS / Defender as External.
                    if (blocked && group == ProcessGroup.External && string.IsNullOrWhiteSpace(path))
                    {
                        group = ProcessGroup.Windows;
                    }

                    var displayName = ResolveDisplayName(path, displayCache);
                    var interest = IsBackgroundInterest(name, path, title, group);
                    rows.Add(new SessionProcessInfo(
                        p.Id,
                        name,
                        ws,
                        priv,
                        path,
                        title,
                        displayName,
                        group,
                        blocked,
                        interest));
                }
            }
            catch
            {
                try { p.Dispose(); } catch { /* ignore */ }
            }
        }

        // Always keep every background-interest / External / titled process; fill remainder by RAM.
        var priority = rows
            .Where(r => r.IsBackgroundInterest
                        || r.Group == ProcessGroup.External
                        || !string.IsNullOrWhiteSpace(r.WindowTitle))
            .ToArray();
        var fillerBudget = Math.Max(0, maxRows - priority.Length);
        var filler = rows
            .Except(priority)
            .OrderByDescending(r => r.WorkingSetBytes)
            .Take(fillerBudget);

        return priority
            .Concat(filler)
            .OrderByDescending(r => r.IsBackgroundInterest)
            .ThenByDescending(r => r.Group == ProcessGroup.External)
            .ThenByDescending(r => r.WorkingSetBytes)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxRows)
            .ToArray();
    }

    public static IReadOnlyList<SessionServiceInfo> ListServices(int maxRows = 400)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);
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
                    var blocked = ExternalWorkloadController.IsServiceHardBlocked(sc.ServiceName);
                    var running = sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending;
                    var interest = IsBackgroundInterest(sc.ServiceName, null, sc.DisplayName, ProcessGroup.External)
                                   || IsBackgroundInterest(sc.DisplayName, null, null, ProcessGroup.External);

                    // Skip quiet Windows noise unless running and stoppable — prefer interest + running.
                    if (!running && !interest)
                    {
                        continue;
                    }

                    if (blocked && !running)
                    {
                        continue;
                    }

                    var canStop = running && !blocked && sc.CanStop;
                    var canStart = !running
                                   && !blocked
                                   && sc.Status is ServiceControllerStatus.Stopped
                                   && sc.StartType is not ServiceStartMode.Disabled;

                    if (!canStop && !canStart && !interest)
                    {
                        continue;
                    }

                    list.Add(new SessionServiceInfo(
                        sc.ServiceName,
                        string.IsNullOrWhiteSpace(sc.DisplayName) ? sc.ServiceName : sc.DisplayName,
                        sc.Status.ToString(),
                        running,
                        canStop,
                        canStart,
                        blocked,
                        interest));
                }
            }
            catch
            {
                try { sc.Dispose(); } catch { /* ignore */ }
            }
        }

        return list
            .OrderByDescending(s => s.IsBackgroundInterest)
            .ThenByDescending(s => s.IsRunning)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(maxRows)
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

    public static SessionStartResult StartSelectedServices(IEnumerable<string> serviceNames)
    {
        var messages = new List<string>();
        var started = 0;
        var blocked = 0;

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
                if (sc.StartType == ServiceStartMode.Disabled)
                {
                    messages.Add("svc disabled: " + name);
                    continue;
                }

                if (sc.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    started++;
                    messages.Add("svc start: " + name);
                }
            }
            catch (Exception ex)
            {
                messages.Add($"svc {name}: {ex.Message}");
            }
        }

        return new SessionStartResult(started, blocked, messages);
    }

    public static bool IsBackgroundInterest(string? name, string? path, string? title, ProcessGroup group)
    {
        if (group == ProcessGroup.External)
        {
            // Prefer user-space third-party always as "interest" for game/work toggles.
            return true;
        }

        // Match name/title/file leaf only — never the full path (avoids username false positives).
        string? leaf = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try { leaf = System.IO.Path.GetFileName(path); }
            catch { /* ignore */ }
        }

        var hay = string.Join(' ', name ?? "", title ?? "", leaf ?? "");
        if (string.IsNullOrWhiteSpace(hay))
        {
            return false;
        }

        foreach (var token in BackgroundTokens)
        {
            if (hay.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return BackgroundRegex().IsMatch(hay);
    }

    private static string? ResolveDisplayName(string? path, Dictionary<string, string?> cache)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        string? display = null;
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrWhiteSpace(vi.FileDescription))
            {
                display = vi.FileDescription.Trim();
            }
        }
        catch
        {
            // ignore
        }

        cache[path] = display;
        return display;
    }

    [GeneratedRegex(@"\b(vpn|lua|helper|overlay|launcher|updater)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BackgroundRegex();
}

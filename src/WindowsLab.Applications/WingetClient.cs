using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WindowsLab.Core;

namespace WindowsLab.Applications;

public enum WingetOutcome
{
    Ok,
    AlreadyInstalled,
    Denied,
    NotFound,
    ElevationRequired,
    Error
}

public sealed record WingetResult(
    WingetOutcome Outcome,
    string Message,
    int ExitCode,
    string? LogPath,
    bool Elevated);

public static class WingetClient
{
    private static string? _resolvedWingetPath;

    public static bool IsAvailable()
    {
        try
        {
            return ResolveWingetPath() is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsPackageInstalled(string wingetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wingetId);
        var r = RunWinget(
            ["list", "--id", wingetId, "--exact", "--disable-interactivity"],
            elevate: false,
            timeoutMs: 60_000);
        if (r.ExitCode != 0)
        {
            return false;
        }

        foreach (var line in r.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = Regex.Split(line.Trim(), @"\s{2,}");
            if (parts.Any(p => string.Equals(p, wingetId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static WingetResult Install(ApplicationDefinition app, bool allowElevate = true)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Evidence is EvidenceGrade.Unknown or EvidenceGrade.Experimental)
        {
            return Persist(new WingetResult(
                WingetOutcome.Denied,
                "Policy: UNKNOWN/EXPERIMENTAL apps are not installable by default.",
                ExitCodes.PolicyBlocked,
                null,
                false), app);
        }

        if (!IsAvailable())
        {
            return Persist(new WingetResult(
                WingetOutcome.NotFound,
                "winget.exe not found on PATH. Install App Installer from Microsoft Store.",
                ExitCodes.Generic,
                null,
                false), app);
        }

        if (InstalledAppDetector.IsInstalled(app) || IsPackageInstalled(app.WingetId))
        {
            return Persist(new WingetResult(
                WingetOutcome.AlreadyInstalled,
                $"Already installed: {app.WingetId}",
                0,
                null,
                false), app);
        }

        var args = BuildInstallArgs(app);
        var first = RunWinget(args, elevate: false, timeoutMs: 600_000);
        if (first.ExitCode == 0)
        {
            return Persist(new WingetResult(WingetOutcome.Ok, "Installed.", 0, null, false), app, first);
        }

        if (LooksAlreadyInstalled(first))
        {
            return Persist(new WingetResult(WingetOutcome.AlreadyInstalled, TrimMsg(first), first.ExitCode, null, false), app, first);
        }

        if (allowElevate && LooksElevationRequired(first))
        {
            var elevated = RunWinget(args, elevate: true, timeoutMs: 600_000);
            if (elevated.ExitCode == 0 || LooksAlreadyInstalled(elevated))
            {
                var outcome = elevated.ExitCode == 0 ? WingetOutcome.Ok : WingetOutcome.AlreadyInstalled;
                return Persist(new WingetResult(outcome, TrimMsg(elevated), elevated.ExitCode, null, true), app, elevated);
            }

            if (elevated.ExitCode == -1073741510 || LooksDenied(elevated))
            {
                return Persist(new WingetResult(
                    WingetOutcome.Denied,
                    string.IsNullOrWhiteSpace(TrimMsg(elevated))
                        ? "UAC cancelled or elevation denied."
                        : TrimMsg(elevated),
                    elevated.ExitCode,
                    null,
                    true), app, elevated);
            }

            return Persist(new WingetResult(
                WingetOutcome.Error,
                string.IsNullOrWhiteSpace(TrimMsg(elevated)) ? "Elevated winget failed." : TrimMsg(elevated),
                elevated.ExitCode,
                null,
                true), app, elevated);
        }

        if (LooksElevationRequired(first))
        {
            return Persist(new WingetResult(
                WingetOutcome.ElevationRequired,
                "Install requires elevation. Re-run with allowElevate or approve UAC.",
                first.ExitCode,
                null,
                false), app, first);
        }

        return Persist(new WingetResult(WingetOutcome.Error, TrimMsg(first), first.ExitCode, null, false), app, first);
    }

    private static string[] BuildInstallArgs(ApplicationDefinition app)
    {
        var list = new List<string>
        {
            "install",
            "--id", app.WingetId,
            "--exact",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity"
        };
        if (app.ScopePreference == AppInstallScope.User)
        {
            list.Add("--scope");
            list.Add("user");
        }

        return list.ToArray();
    }

    private static WingetResult Persist(WingetResult result, ApplicationDefinition app, ProcessRun? run = null)
    {
        try
        {
            ConfigChannels.EnsureRuntimeFolders();
            var dir = Path.Combine(ConfigChannels.MachineRoot, "reports", "app-installs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Sanitize(app.Id)}.json");
            var payload = new
            {
                utc = DateTimeOffset.UtcNow,
                app.Id,
                app.WingetId,
                result.Outcome,
                result.Message,
                result.ExitCode,
                result.Elevated,
                stdout = run?.StdOut,
                stderr = run?.StdErr
            };
            File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonDefaults.Options));
            return result with { LogPath = path };
        }
        catch (Exception ex)
        {
            return result with { Message = result.Message + " (log failed: " + ex.Message + ")" };
        }
    }

    private static string Sanitize(string id) =>
        string.Join("_", id.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    private static bool LooksAlreadyInstalled(ProcessRun r) =>
        r.StdOut.Contains("already installed", StringComparison.OrdinalIgnoreCase)
        || r.StdErr.Contains("already installed", StringComparison.OrdinalIgnoreCase)
        || r.ExitCode is -1978335189 or -1978335135;

    private static bool LooksElevationRequired(ProcessRun r) =>
        r.StdOut.Contains("elevation", StringComparison.OrdinalIgnoreCase)
        || r.StdErr.Contains("elevation", StringComparison.OrdinalIgnoreCase)
        || r.StdOut.Contains("administrator", StringComparison.OrdinalIgnoreCase)
        || r.StdErr.Contains("administrator", StringComparison.OrdinalIgnoreCase)
        || r.ExitCode is -1978335215 or -1978335185;

    private static bool LooksDenied(ProcessRun r) =>
        r.StdErr.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
        || r.StdOut.Contains("cancelled", StringComparison.OrdinalIgnoreCase);

    private static string TrimMsg(ProcessRun r)
    {
        var text = string.IsNullOrWhiteSpace(r.StdErr) ? r.StdOut : r.StdErr;
        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length <= 400 ? text : text[..400] + "…";
    }

    private sealed record ProcessRun(int ExitCode, string StdOut, string StdErr);

    private static string? ResolveWingetPath()
    {
        if (_resolvedWingetPath is not null)
        {
            return File.Exists(_resolvedWingetPath) ? _resolvedWingetPath : null;
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(local))
        {
            _resolvedWingetPath = local;
            return local;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), "winget.exe");
                if (File.Exists(candidate))
                {
                    _resolvedWingetPath = candidate;
                    return candidate;
                }
            }
            catch
            {
                // skip bad PATH entries
            }
        }

        return null;
    }

    private static ProcessRun RunWinget(IReadOnlyList<string> args, bool elevate, int timeoutMs)
    {
        var winget = ResolveWingetPath()
            ?? throw new InvalidOperationException("winget.exe not found.");

        if (elevate)
        {
            var outFile = Path.Combine(Path.GetTempPath(), "wl-winget-out-" + Guid.NewGuid().ToString("N") + ".txt");
            var errFile = Path.Combine(Path.GetTempPath(), "wl-winget-err-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var argLine = string.Join(' ', args.Select(Quote));
                var cmd = $"/c \"\"{winget}\" {argLine} > \"{outFile}\" 2> \"{errFile}\"\"";
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start elevated winget.");
                if (!proc.WaitForExit(timeoutMs))
                {
                    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    return new ProcessRun(-1, "", "winget timed out");
                }

                var stdout = File.Exists(outFile) ? File.ReadAllText(outFile) : "";
                var stderr = File.Exists(errFile) ? File.ReadAllText(errFile) : "";
                return new ProcessRun(proc.ExitCode, stdout, stderr);
            }
            finally
            {
                try { if (File.Exists(outFile)) File.Delete(outFile); } catch { /* ignore */ }
                try { if (File.Exists(errFile)) File.Delete(errFile); } catch { /* ignore */ }
            }
        }

        var normal = new ProcessStartInfo
        {
            FileName = winget,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var a in args)
        {
            normal.ArgumentList.Add(a);
        }

        using var p = Process.Start(normal)
            ?? throw new InvalidOperationException("Failed to start winget.");
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sbOut.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) sbErr.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new ProcessRun(-1, sbOut.ToString(), "winget timed out");
        }

        return new ProcessRun(p.ExitCode, sbOut.ToString(), sbErr.ToString());
    }

    private static string Quote(string arg) =>
        arg.Contains(' ', StringComparison.Ordinal) || arg.Contains('"', StringComparison.Ordinal)
            ? "\"" + arg.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : arg;
}

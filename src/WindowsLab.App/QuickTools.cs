using System.Diagnostics;
using System.IO;
using System.Text;
using WindowsLab.Applications;
using WindowsLab.Core;

namespace WindowsLab.App;

public sealed record BgInfoReport(
    bool Ok,
    string Summary,
    string Details,
    string LogPath,
    bool NeedsInstall,
    IReadOnlyList<string> WallpaperConflicts);

public static class QuickTools
{
    public static string DownloadsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public static string ToolsDir
    {
        get
        {
            var nextToApp = Path.Combine(AppContext.BaseDirectory, "tools");
            if (Directory.Exists(nextToApp))
            {
                return nextToApp;
            }

            var repo = Path.Combine(FindRepoRoot() ?? "", "tools");
            if (Directory.Exists(repo))
            {
                return repo;
            }

            var user = Path.Combine(ConfigChannels.UserRoot, "tools");
            Directory.CreateDirectory(user);
            return user;
        }
    }

    public static string BgInfoLogPath =>
        Path.Combine(ConfigChannels.UserRoot, "logs", "bginfo.log");

    private static readonly (string ProcessName, string Label)[] WallpaperControllers =
    [
        ("Lively", "Lively Wallpaper"),
        ("Livelycu", "Lively Wallpaper"),
        ("wallpaper32", "Wallpaper Engine"),
        ("wallpaper64", "Wallpaper Engine"),
        ("wallpaper_engine", "Wallpaper Engine"),
        ("DisplayFusion", "DisplayFusion"),
        ("Rainmeter", "Rainmeter"),
        ("WallpaperCore", "Wallpaper Core"),
        ("WallpaperChanger", "Wallpaper Changer"),
        ("DeskScapes", "DeskScapes"),
        ("Fences", "Fences"),
        ("WallpaperFx", "WallpaperFX"),
        ("MultimonitorWallpaper", "MultiMonitor Wallpaper"),
    ];

    public static string? FindActivateWin()
    {
        string[] names = ["activate.win", "activate.win.cmd", "activate.cmd", "Activate.win"];
        foreach (var dir in CandidateToolDirs())
        {
            foreach (var name in names)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    public static string? FindBgInfo()
    {
        string[] names = ["Bginfo64.exe", "Bginfo.exe", "BGInfo64.exe", "BGInfo.exe"];
        foreach (var dir in CandidateToolDirs())
        {
            foreach (var name in names)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        var programFiles = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BGInfo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "BGInfo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages")
        };
        foreach (var root in programFiles)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                var hit = Directory.EnumerateFiles(root, "Bginfo*.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null)
                {
                    return hit;
                }
            }
            catch
            {
                // ignore ACL noise
            }
        }

        return null;
    }

    public static IReadOnlyList<string> DetectWallpaperConflicts()
    {
        var hits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var (processName, label) in WallpaperControllers)
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    hits.Add(label);
                }
            }
        }
        catch
        {
            // best effort
        }

        return hits.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static BgInfoReport ProbeBgInfo()
    {
        var log = EnsureBgInfoLog();
        var conflicts = DetectWallpaperConflicts();
        var existing = FindBgInfo();
        var sb = new StringBuilder();
        sb.AppendLine(Loc.IsEnglish ? "BGInfo diagnostic" : "Diagnóstico BGInfo");
        sb.AppendLine("UTC: " + DateTimeOffset.UtcNow.ToString("u"));
        sb.AppendLine("Exe: " + (existing ?? (Loc.IsEnglish ? "(not found)" : "(no encontrado)")));
        sb.AppendLine("winget: " + (WingetClient.IsAvailable() ? "yes" : "no"));
        if (conflicts.Count == 0)
        {
            sb.AppendLine(Loc.IsEnglish
                ? "Wallpaper controllers: none detected"
                : "Controladores de fondo: ninguno detectado");
        }
        else
        {
            sb.AppendLine((Loc.IsEnglish ? "Wallpaper controllers running: " : "Controladores de fondo activos: ")
                          + string.Join(", ", conflicts));
            sb.AppendLine(Loc.IsEnglish
                ? "Warning: these apps often lock the wallpaper; BGInfo may open but fail to paint or appear to do nothing."
                : "Aviso: estas apps suelen bloquear el fondo; BGInfo puede abrirse pero no pintar nada.");
        }

        AppendBgInfoLog(log, sb.ToString());
        StartupLog.Write("BGInfo.probe", existing ?? "missing");

        return new BgInfoReport(
            Ok: true,
            Summary: existing is null
                ? (Loc.IsEnglish ? "BGInfo is not installed yet." : "BGInfo aún no está instalado.")
                : (Loc.IsEnglish ? "BGInfo found." : "BGInfo encontrado."),
            Details: sb.ToString().TrimEnd(),
            LogPath: log,
            NeedsInstall: existing is null,
            WallpaperConflicts: conflicts);
    }

    public static BgInfoReport LaunchOrInstallBgInfo(bool allowInstall)
    {
        var log = EnsureBgInfoLog();
        var probe = ProbeBgInfo();
        var existing = FindBgInfo();

        if (existing is not null)
        {
            return LaunchBgInfo(existing, probe.WallpaperConflicts, log);
        }

        if (!allowInstall)
        {
            var msg = Loc.IsEnglish
                ? "BGInfo not found. Confirm install via winget, or use Docs BGInfo."
                : "BGInfo no encontrado. Confirma instalar con winget, o usa Docs BGInfo.";
            AppendBgInfoLog(log, "SKIP install — " + msg);
            return probe with { Ok = false, Summary = msg, NeedsInstall = true };
        }

        if (!WingetClient.IsAvailable())
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://learn.microsoft.com/sysinternals/downloads/bginfo",
                    UseShellExecute = true
                });
                var msg = Loc.IsEnglish
                    ? "winget unavailable — opened official BGInfo download page."
                    : "winget no disponible — abierta la página oficial de BGInfo.";
                AppendBgInfoLog(log, msg);
                StartupLog.Write("BGInfo.docs", msg);
                return new BgInfoReport(true, msg, probe.Details + "\n\n" + msg, log, NeedsInstall: true, probe.WallpaperConflicts);
            }
            catch (Exception ex)
            {
                AppendBgInfoLog(log, "FAIL open docs: " + ex);
                StartupLog.Write("BGInfo.fail", ex.Message);
                return new BgInfoReport(false, ex.Message, probe.Details + "\n\n" + ex, log, true, probe.WallpaperConflicts);
            }
        }

        AppendBgInfoLog(log, "Installing Microsoft.Sysinternals.BGInfo via winget…");
        StartupLog.Write("BGInfo.install.begin");
        var app = new ApplicationDefinition(
            "app.tool.bginfo",
            "BGInfo",
            "Sysinternals desktop info",
            "Microsoft.Sysinternals.BGInfo",
            "tool",
            EvidenceGrade.Official,
            ["https://learn.microsoft.com/sysinternals/downloads/bginfo"],
            RiskLevel.Low,
            new AppAxes(0.7, 0.8, 0.8, 0.5, 0.6),
            ["balanced", "developer"],
            AppInstallScope.User,
            ["BGInfo", "Bginfo"]);

        var result = WingetClient.Install(app, allowElevate: true);
        AppendBgInfoLog(log, $"winget outcome={result.Outcome} msg={result.Message} log={result.LogPath}");
        StartupLog.Write("BGInfo.install", $"{result.Outcome}:{result.Message}");
        if (result.Outcome is not (WingetOutcome.Ok or WingetOutcome.AlreadyInstalled))
        {
            var fail = $"BGInfo install: {result.Outcome} — {result.Message}"
                       + (result.LogPath is null ? "" : "\nwinget log: " + result.LogPath);
            return new BgInfoReport(false, fail, probe.Details + "\n\n" + fail, log, true, probe.WallpaperConflicts);
        }

        existing = FindBgInfo();
        if (existing is null)
        {
            var msg = Loc.IsEnglish
                ? "BGInfo installed, but Bginfo64.exe was not found yet. Open it from Start, or press BGInfo again.\nLog: "
                  + log
                : "BGInfo instalado, pero no se encontró Bginfo64.exe. Ábrelo desde Inicio o pulsa BGInfo de nuevo.\nLog: "
                  + log;
            AppendBgInfoLog(log, msg);
            return new BgInfoReport(true, msg, probe.Details + "\n\n" + msg, log, false, probe.WallpaperConflicts);
        }

        return LaunchBgInfo(existing, probe.WallpaperConflicts, log);
    }

    private static BgInfoReport LaunchBgInfo(string exe, IReadOnlyList<string> conflicts, string log)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            });

            var warn = conflicts.Count == 0
                ? ""
                : (Loc.IsEnglish
                    ? $"\n\nConflicts detected: {string.Join(", ", conflicts)}.\nIf the desktop does not change, pause/close that wallpaper app, then run BGInfo again and click Apply."
                    : $"\n\nConflictos detectados: {string.Join(", ", conflicts)}.\nSi el escritorio no cambia, pausa/cierra esa app de fondo, vuelve a abrir BGInfo y pulsa Apply.");

            var summary = (Loc.IsEnglish ? "BGInfo launched: " : "BGInfo lanzado: ") + exe + warn;
            AppendBgInfoLog(log, "LAUNCH ok " + exe + (conflicts.Count == 0 ? "" : " conflicts=" + string.Join("|", conflicts)));
            StartupLog.Write("BGInfo.launch", exe);
            return new BgInfoReport(
                true,
                summary,
                (Loc.IsEnglish ? "Launched" : "Lanzado") + ": " + exe + warn + "\n\nLog: " + log,
                log,
                NeedsInstall: false,
                conflicts);
        }
        catch (Exception ex)
        {
            AppendBgInfoLog(log, "LAUNCH fail: " + ex);
            StartupLog.Write("BGInfo.launch.fail", ex.Message);
            return new BgInfoReport(
                false,
                (Loc.IsEnglish ? "Could not start BGInfo: " : "No se pudo iniciar BGInfo: ") + ex.Message,
                ex.ToString() + "\n\nLog: " + log,
                log,
                false,
                conflicts);
        }
    }

    private static string EnsureBgInfoLog()
    {
        try
        {
            var dir = Path.GetDirectoryName(BgInfoLogPath)!;
            Directory.CreateDirectory(dir);
            return BgInfoLogPath;
        }
        catch
        {
            return BgInfoLogPath;
        }
    }

    private static void AppendBgInfoLog(string path, string text)
    {
        try
        {
            File.AppendAllText(path,
                DateTimeOffset.Now.ToString("O") + "\n" + text.TrimEnd() + "\n---\n");
        }
        catch
        {
            // never throw from logging
        }
    }

    public static (bool Ok, string Message) LaunchActivateWindows()
    {
        var script = FindActivateWin();
        if (script is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = script,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(script)
                });
                return (true, "Lanzado: " + script);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:activation",
                UseShellExecute = true
            });
            return (true, "Sin activate.win local → abriendo Configuración > Activación. Coloca tu script en tools\\activate.win para usarlo.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static IEnumerable<string> CandidateToolDirs()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "tools");
        yield return Path.Combine(ConfigChannels.UserRoot, "tools");
        var repo = FindRepoRoot();
        if (repo is not null)
        {
            yield return Path.Combine(repo, "tools");
        }
    }

    private static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WindowsLab.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}

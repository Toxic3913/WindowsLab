using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using WindowsLab.Core;

namespace WindowsLab.App;

/// <summary>
/// BGInfo owns the desktop wallpaper bitmap. Wallpaper Engine / Lively / etc. also own it —
/// they cannot paint the same surface. We pause conflicts on enable and restore on disable.
/// </summary>
public static class BgInfoService
{
    private const uint SpiSetDeskWallpaper = 0x0014;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendWinIniChange = 0x02;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string StatePath =>
        Path.Combine(ConfigChannels.UserRoot, "bginfo-state.json");

    public static BgInfoSessionState? LoadState()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<BgInfoSessionState>(File.ReadAllText(StatePath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsMarkedActive => LoadState()?.Active == true;

    public static BgInfoReport Enable(bool allowInstall, bool pauseConflicts)
    {
        var log = QuickTools.BgInfoLogPath;
        var probe = QuickTools.ProbeBgInfo();
        var steps = new StringBuilder();
        steps.AppendLine(probe.Details);
        steps.AppendLine();

        // Incompatible by design when another app owns the wallpaper.
        if (probe.WallpaperConflicts.Count > 0)
        {
            steps.AppendLine(Loc.IsEnglish
                ? "Note: BGInfo replaces the Windows wallpaper. Wallpaper Engine / Lively cannot stay fully active at the same time."
                : "Nota: BGInfo sustituye el fondo de Windows. Wallpaper Engine / Lively no pueden seguir activos al completo.");
        }

        var wallpaper = CaptureWallpaper();
        SaveState(new BgInfoSessionState(
            Active: false,
            EnabledUtc: DateTimeOffset.UtcNow,
            WallpaperPath: wallpaper.Path,
            WallpaperStyle: wallpaper.Style,
            TileWallpaper: wallpaper.Tile,
            PausedLabels: []));

        string[] paused = [];
        if (pauseConflicts && probe.WallpaperConflicts.Count > 0)
        {
            paused = PauseWallpaperControllers(probe.WallpaperConflicts);
            steps.AppendLine((Loc.IsEnglish ? "Paused: " : "Pausado: ")
                             + (paused.Length == 0
                                 ? (Loc.IsEnglish ? "(could not pause; close them manually)" : "(no se pudo pausar; ciérralos a mano)")
                                 : string.Join(", ", paused)));
        }

        SaveState(new BgInfoSessionState(
            Active: true,
            EnabledUtc: DateTimeOffset.UtcNow,
            WallpaperPath: wallpaper.Path,
            WallpaperStyle: wallpaper.Style,
            TileWallpaper: wallpaper.Tile,
            PausedLabels: paused));

        AppendLog(log, "ENABLE wallpaperSaved=" + (wallpaper.Path ?? "(none)") + " paused=" + string.Join("|", paused));
        StartupLog.Write("BGInfo.enable", "pause=" + pauseConflicts);

        var launch = QuickTools.LaunchOrInstallBgInfo(allowInstall);
        steps.AppendLine();
        steps.AppendLine(launch.Details);
        steps.AppendLine();
        steps.AppendLine(Loc.IsEnglish
            ? "In BGInfo: click Apply. Use Desactivar BGInfo later to restore your wallpaper / resume Wallpaper Engine."
            : "En BGInfo: pulsa Apply. Usa Desactivar BGInfo después para restaurar el fondo / reanudar Wallpaper Engine.");

        return new BgInfoReport(
            launch.Ok,
            launch.Summary,
            steps.ToString().TrimEnd(),
            log,
            launch.NeedsInstall,
            probe.WallpaperConflicts);
    }

    public static BgInfoReport Disable(bool resumeConflicts)
    {
        var log = QuickTools.BgInfoLogPath;
        var state = LoadState();
        var conflicts = QuickTools.DetectWallpaperConflicts();
        var sb = new StringBuilder();
        sb.AppendLine(Loc.IsEnglish ? "Disabling BGInfo…" : "Desactivando BGInfo…");

        var killed = StopBgInfoProcesses();
        sb.AppendLine((Loc.IsEnglish ? "BGInfo processes stopped: " : "Procesos BGInfo detenidos: ") + killed);

        var restored = false;
        if (state is not null && !string.IsNullOrWhiteSpace(state.WallpaperPath) && File.Exists(state.WallpaperPath))
        {
            restored = RestoreWallpaper(state.WallpaperPath, state.WallpaperStyle, state.TileWallpaper);
            sb.AppendLine(restored
                ? (Loc.IsEnglish ? "Wallpaper restored: " : "Fondo restaurado: ") + state.WallpaperPath
                : (Loc.IsEnglish ? "Could not restore wallpaper file." : "No se pudo restaurar el archivo de fondo."));
        }
        else
        {
            // Clear BGInfo-generated wallpaper so Wallpaper Engine can reclaim the surface.
            restored = RestoreWallpaper(string.Empty, state?.WallpaperStyle, state?.TileWallpaper);
            sb.AppendLine(Loc.IsEnglish
                ? "No saved wallpaper path — cleared desktop wallpaper so controllers can take over."
                : "Sin ruta de fondo guardada — se limpió el wallpaper para que los controladores puedan recuperar el escritorio.");
        }

        string[] resumed = [];
        if (resumeConflicts)
        {
            var labels = state?.PausedLabels is { Count: > 0 } p
                ? p
                : conflicts;
            resumed = ResumeWallpaperControllers(labels);
            sb.AppendLine((Loc.IsEnglish ? "Resumed: " : "Reanudado: ")
                          + (resumed.Length == 0
                              ? (Loc.IsEnglish ? "(none / open Wallpaper Engine manually)" : "(ninguno / abre Wallpaper Engine a mano)")
                              : string.Join(", ", resumed)));
        }

        SaveState(new BgInfoSessionState(
            Active: false,
            EnabledUtc: state?.EnabledUtc,
            WallpaperPath: state?.WallpaperPath,
            WallpaperStyle: state?.WallpaperStyle,
            TileWallpaper: state?.TileWallpaper,
            PausedLabels: []));

        AppendLog(log, sb.ToString());
        StartupLog.Write("BGInfo.disable", $"killed={killed}; restored={restored}");

        var ok = killed > 0 || restored || resumed.Length > 0 || state is not null;
        var summary = Loc.IsEnglish
            ? $"BGInfo disabled. Processes stopped: {killed}. Wallpaper restore: {(restored ? "yes" : "no")}."
            : $"BGInfo desactivado. Procesos detenidos: {killed}. Restaurar fondo: {(restored ? "sí" : "no")}.";

        return new BgInfoReport(ok, summary, sb.ToString().TrimEnd() + "\n\nLog: " + log, log, false, conflicts);
    }

    public static int StopBgInfoProcesses()
    {
        var count = 0;
        foreach (var name in new[] { "Bginfo64", "Bginfo", "BGInfo64", "BGInfo" })
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        using (p)
                        {
                            if (!p.HasExited)
                            {
                                p.Kill(entireProcessTree: true);
                                p.WaitForExit(5_000);
                                count++;
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        return count;
    }

    private static WallpaperSnapshot CaptureWallpaper()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var path = key?.GetValue("Wallpaper") as string;
            var style = key?.GetValue("WallpaperStyle") as string;
            var tile = key?.GetValue("TileWallpaper") as string;
            return new WallpaperSnapshot(path, style, tile);
        }
        catch
        {
            return new WallpaperSnapshot(null, null, null);
        }
    }

    private static bool RestoreWallpaper(string path, string? style, string? tile)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
            {
                if (key is not null)
                {
                    if (style is not null)
                    {
                        key.SetValue("WallpaperStyle", style);
                    }

                    if (tile is not null)
                    {
                        key.SetValue("TileWallpaper", tile);
                    }
                }
            }

            return SystemParametersInfo(SpiSetDeskWallpaper, 0, path ?? string.Empty, SpifUpdateIniFile | SpifSendWinIniChange);
        }
        catch (Exception ex)
        {
            AppendLog(QuickTools.BgInfoLogPath, "RestoreWallpaper fail: " + ex.Message);
            return false;
        }
    }

    private static string[] PauseWallpaperControllers(IReadOnlyList<string> labels)
    {
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in labels)
        {
            if (label.Contains("Wallpaper Engine", StringComparison.OrdinalIgnoreCase))
            {
                if (SendWallpaperEngineControl("pause"))
                {
                    done.Add("Wallpaper Engine");
                }
            }
            else if (label.Contains("Lively", StringComparison.OrdinalIgnoreCase))
            {
                if (TryCloseProcessNames("Lively", "Livelycu"))
                {
                    done.Add("Lively Wallpaper");
                }
            }
        }

        return done.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] ResumeWallpaperControllers(IReadOnlyList<string> labels)
    {
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in labels)
        {
            if (label.Contains("Wallpaper Engine", StringComparison.OrdinalIgnoreCase))
            {
                if (SendWallpaperEngineControl("play") || TryStartWallpaperEngine())
                {
                    done.Add("Wallpaper Engine");
                }
            }
            // Lively: user restarts from Start / tray — we do not force-relaunch arbitrary install paths.
        }

        return done.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool SendWallpaperEngineControl(string command)
    {
        foreach (var name in new[] { "wallpaper64", "wallpaper32", "wallpaper_engine" })
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        using (p)
                        {
                            var exe = p.MainModule?.FileName;
                            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                            {
                                continue;
                            }

                            var psi = new ProcessStartInfo
                            {
                                FileName = exe,
                                Arguments = "-control " + command,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = Path.GetDirectoryName(exe)
                            };
                            using var child = Process.Start(psi);
                            child?.WaitForExit(8_000);
                            AppendLog(QuickTools.BgInfoLogPath, $"WE -control {command} via {exe} exit={child?.ExitCode}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog(QuickTools.BgInfoLogPath, $"WE control fail: {ex.Message}");
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        return false;
    }

    private static bool TryStartWallpaperEngine()
    {
        try
        {
            // Steam appid 431960
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://rungameid/431960",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCloseProcessNames(params string[] names)
    {
        var any = false;
        foreach (var name in names)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        using (p)
                        {
                            if (!p.CloseMainWindow())
                            {
                                p.Kill(entireProcessTree: true);
                            }

                            p.WaitForExit(5_000);
                            any = true;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        return any;
    }

    private static void SaveState(BgInfoSessionState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // best effort
        }
    }

    private static void AppendLog(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + "\n" + text.TrimEnd() + "\n---\n");
        }
        catch
        {
            // never throw from logging
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    private sealed record WallpaperSnapshot(string? Path, string? Style, string? Tile);
}

public sealed record BgInfoSessionState(
    bool Active,
    DateTimeOffset? EnabledUtc,
    string? WallpaperPath,
    string? WallpaperStyle,
    string? TileWallpaper,
    IReadOnlyList<string> PausedLabels);

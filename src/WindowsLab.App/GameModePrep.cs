using System.Diagnostics;
using System.IO;
using WindowsLab.Core;

namespace WindowsLab.App;

/// <summary>
/// Pre-game prep: curated workload stop + SCSKiller detect/launch.
/// Never empties standby / SysMain / Defender / Search (D028 + product policy).
/// </summary>
public static class GameModePrep
{
    /// <summary>Background apps safe to stop before playing (not Steam/Epic/Riot/Discord).</summary>
    public static readonly string[] DistractorWorkloadIds =
    [
        "workload.overwolf",
        "workload.libreoffice",
        "workload.xbox-gaming"
    ];

    public static readonly string[] GameModeTweakIds =
    [
        "gaming.game-mode-on",
        "gaming.game-dvr-off",
        "gaming.gamebar-tips-off",
        "gaming.mouse-accel-off",
        "keyboard.stickykeys-hotkey-off"
    ];

    public const string ScsKillerReleasesUrl =
        "https://github.com/BlueHeisenberg/SCSKiller/releases/latest";

    public const string ScsKillerRepoUrl =
        "https://github.com/BlueHeisenberg/SCSKiller";

    public static IReadOnlyList<ExternalWorkloadDefinition> GetDistractors(
        IReadOnlyList<ExternalWorkloadDefinition> all) =>
        all.Where(w => DistractorWorkloadIds.Contains(w.Id, StringComparer.OrdinalIgnoreCase))
            .ToArray();

    public static (int Processes, int Services, IReadOnlyList<string> Titles) StopDistractors(
        IReadOnlyList<ExternalWorkloadDefinition> all)
    {
        var proc = 0;
        var svc = 0;
        var titles = new List<string>();
        foreach (var def in GetDistractors(all))
        {
            var status = ExternalWorkloadController.Detect(def);
            if (!status.Active)
            {
                continue;
            }

            var result = ExternalWorkloadController.Stop(def);
            proc += result.ProcessesStopped;
            svc += result.ServicesStopped;
            titles.Add(def.Title);
        }

        return (proc, svc, titles);
    }

    public static string? FindScsKillerExe()
    {
        foreach (var candidate in EnumerateScsKillerPaths())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool TryLaunchScsKiller()
    {
        var exe = FindScsKillerExe();
        if (exe is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateScsKillerPaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "SCSKiller", "SCSKiller.exe");
        yield return Path.Combine(local, "Programs", "SCSKiller", "SCSKiller.exe");

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(pf, "SCSKiller", "SCSKiller.exe");

        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(pf86, "SCSKiller", "SCSKiller.exe");
    }
}

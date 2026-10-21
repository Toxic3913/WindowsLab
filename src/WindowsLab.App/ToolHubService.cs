using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using WindowsLab.Applications;
using WindowsLab.Core;

namespace WindowsLab.App;

public static class ToolHubService
{
    public static IReadOnlyList<ToolHubEntry> LoadEntries()
    {
        var dir = CatalogLocator.FindToolHubDirectory();
        if (dir is null)
        {
            StartupLog.Write("ToolHub.load", "catalog/tool-hub not found");
            return [];
        }

        try
        {
            return ToolHubCatalogLoader.LoadDirectory(dir);
        }
        catch (Exception ex)
        {
            StartupLog.Write("ToolHub.load.fail", ex.Message);
            return [];
        }
    }

    public static bool IsPresent(
        ToolHubEntry entry,
        IReadOnlyList<ApplicationDefinition> apps,
        IReadOnlyList<InstalledAppFact>? installedFacts = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (AnyLaunchHintExists(entry))
        {
            return true;
        }

        if (entry.Kind == ToolHubKind.Winget && !string.IsNullOrWhiteSpace(entry.CatalogAppId))
        {
            var app = apps.FirstOrDefault(a =>
                string.Equals(a.Id, entry.CatalogAppId, StringComparison.OrdinalIgnoreCase));
            if (app is not null)
            {
                // ARP scan only on UI refresh — avoid synchronous `winget list` freezes.
                return InstalledAppDetector.IsInstalled(app, installedFacts);
            }
        }

        return false;
    }

    public static ApplicationDefinition? ResolveCatalogApp(
        ToolHubEntry entry,
        IReadOnlyList<ApplicationDefinition> apps)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.CatalogAppId))
        {
            return null;
        }

        return apps.FirstOrDefault(a =>
            string.Equals(a.Id, entry.CatalogAppId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryLaunch(ToolHubEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        foreach (var candidate in ExpandLaunchHints(entry.LaunchExeHints))
        {
            try
            {
                if (!LooksLikeFilePath(candidate) || File.Exists(candidate))
                {
                    Process.Start(new ProcessStartInfo { FileName = candidate, UseShellExecute = true });
                    return true;
                }
            }
            catch
            {
                // try next
            }
        }

        return false;
    }

    public static bool TryOpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryOpenSteamApp(string? steamAppId)
    {
        if (string.IsNullOrWhiteSpace(steamAppId))
        {
            return false;
        }

        if (TryOpenUrl("steam://store/" + steamAppId))
        {
            return true;
        }

        return TryOpenUrl("https://store.steampowered.com/app/" + steamAppId + "/");
    }

    public static bool TryOpenSettings(string? settingsUri)
    {
        if (string.IsNullOrWhiteSpace(settingsUri))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = settingsUri, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IEnumerable<string> ExpandLaunchHints(IReadOnlyList<string> hints)
    {
        foreach (var raw in hints)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            yield return ExpandEnv(raw.Trim());
        }
    }

    private static bool AnyLaunchHintExists(ToolHubEntry entry) =>
        ExpandLaunchHints(entry.LaunchExeHints).Any(p => LooksLikeFilePath(p) && File.Exists(p));

    private static bool LooksLikeFilePath(string path) =>
        path.Contains('\\', StringComparison.Ordinal)
        || path.Contains('/', StringComparison.Ordinal)
        || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private static string ExpandEnv(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        // ExpandEnvironmentVariables does not expand %ProgramFiles(x86)% on all hosts reliably via that name.
        expanded = expanded.Replace(
            "%ProgramFiles(x86)%",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            StringComparison.OrdinalIgnoreCase);
        expanded = Regex.Replace(
            expanded,
            "%LOCALAPPDATA%",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expanded = Regex.Replace(
            expanded,
            "%ProgramFiles%",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return expanded;
    }
}

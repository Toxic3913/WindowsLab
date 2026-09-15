using System.Text.RegularExpressions;
using Microsoft.Win32;
using WindowsLab.Core;

namespace WindowsLab.Applications;

public static class InstalledAppDetector
{
    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    public static IReadOnlyList<InstalledAppFact> Scan()
    {
        var list = new List<InstalledAppFact>();
        foreach (var root in UninstallRoots)
        {
            Collect(Registry.LocalMachine, root, "HKLM", list);
            Collect(Registry.CurrentUser, root, "HKCU", list);
        }

        return list;
    }

    public static bool IsInstalled(ApplicationDefinition app, IReadOnlyList<InstalledAppFact>? facts = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        facts ??= Scan();
        if (app.DetectNames.Count == 0)
        {
            return false;
        }

        return app.DetectNames.Any(name =>
            facts.Any(f => NameMatches(f.DisplayName, name)));
    }

    public static bool NameMatches(string displayName, string detectName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(detectName))
        {
            return false;
        }

        if (string.Equals(displayName.Trim(), detectName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Word-boundary match: "Git" matches "Git" / "Git for Windows", not "GitHub Desktop".
        var pattern = @"\b" + Regex.Escape(detectName.Trim()) + @"\b";
        return Regex.IsMatch(displayName, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void Collect(RegistryKey hive, string root, string source, List<InstalledAppFact> list)
    {
        using var key = hive.OpenSubKey(root);
        if (key is null)
        {
            return;
        }

        foreach (var subName in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(subName);
            if (sub is null)
            {
                continue;
            }

            var display = sub.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(display))
            {
                continue;
            }

            var publisher = sub.GetValue("Publisher") as string;
            list.Add(new InstalledAppFact(display.Trim(), publisher, source));
        }
    }
}

using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;

namespace WindowsLab.Setup;

/// <summary>
/// Add/Remove Programs (ARP) registration and shared uninstall helpers (D030).
/// </summary>
internal static class InstallRegistration
{
    public const string ProductName = "WindowsLab";
    public const string Publisher = "WindowsLab";
    public const string UninstallKeyName = "WindowsLab";
    public const string ProductUrl = "https://github.com/Toxic3913/WindowsLab";
    public const string ReleasesUrl = "https://github.com/Toxic3913/WindowsLab/releases";

    private static string UninstallKeyPath =>
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + UninstallKeyName;

    public static void Register(
        string installDir,
        string version,
        string uninstallExePath,
        string? displayIconPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(uninstallExePath);

        installDir = Path.GetFullPath(installDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        uninstallExePath = Path.GetFullPath(uninstallExePath);
        displayIconPath ??= Path.Combine(installDir, "WindowsLab.exe");

        using var key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath)
                        ?? throw new InvalidOperationException("No se pudo crear la clave de desinstalación (¿admin?).");

        key.SetValue("DisplayName", ProductName, RegistryValueKind.String);
        key.SetValue("DisplayVersion", version, RegistryValueKind.String);
        key.SetValue("Publisher", Publisher, RegistryValueKind.String);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), RegistryValueKind.String);
        key.SetValue("DisplayIcon", displayIconPath + ",0", RegistryValueKind.String);
        key.SetValue("UninstallString", $"\"{uninstallExePath}\"", RegistryValueKind.String);
        key.SetValue("QuietUninstallString", $"\"{uninstallExePath}\" --silent", RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("URLInfoAbout", ProductUrl, RegistryValueKind.String);
        key.SetValue("HelpLink", ReleasesUrl, RegistryValueKind.String);
        key.SetValue("Comments", "Audit, configure, and maintain Windows 11", RegistryValueKind.String);

        var sizeKb = EstimateSizeKb(installDir);
        if (sizeKb > 0 && sizeKb <= int.MaxValue)
        {
            key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
        }
    }

    public static void Unregister()
    {
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // best effort
        }
    }

    public static string? TryReadInstallLocation()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath);
            return key?.GetValue("InstallLocation") as string;
        }
        catch
        {
            return null;
        }
    }

    public static long EstimateSizeKb(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            long bytes = 0;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                }
                catch
                {
                    // skip locked/missing
                }
            }

            return Math.Max(1, bytes / 1024);
        }
        catch
        {
            return 0;
        }
    }

    public static void RemoveShortcuts()
    {
        var links = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "WindowsLab.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "WindowsLab.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "WindowsLab", "WindowsLab.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Desinstalar WindowsLab.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "WindowsLab", "Desinstalar WindowsLab.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Uninstall WindowsLab.lnk"),
        };

        foreach (var link in links)
        {
            try
            {
                if (File.Exists(link))
                {
                    File.Delete(link);
                }
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            var group = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "WindowsLab");
            if (Directory.Exists(group) && !Directory.EnumerateFileSystemEntries(group).Any())
            {
                Directory.Delete(group);
            }
        }
        catch
        {
            // ignore
        }
    }

    public static void CreateUninstallShortcut(string uninstallExe, bool spanish)
    {
        try
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            var group = Path.Combine(programs, "WindowsLab");
            Directory.CreateDirectory(group);
            var name = spanish ? "Desinstalar WindowsLab.lnk" : "Uninstall WindowsLab.lnk";
            CreateShortcut(uninstallExe, group, name, spanish ? "Desinstalar WindowsLab" : "Uninstall WindowsLab");
        }
        catch
        {
            // non-fatal
        }
    }

    public static void CreateShortcut(string targetExe, string folder, string linkName, string? description = null)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var link = Path.Combine(folder, linkName);
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            var shortcut = shell.CreateShortcut(link);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
            shortcut.Description = description ?? ProductName;
            shortcut.Save();
        }
        catch
        {
            // non-fatal
        }
    }

    public static void StopProductProcesses()
    {
        foreach (var name in new[] { "WindowsLab", "WindowsLab.Worker", "windowslab-cli", "WindowsLab-Setup" })
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
                                p.WaitForExit(8_000);
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
    }

    public static string ReadProductVersion(string installDir)
    {
        try
        {
            var exe = Path.Combine(installDir, "WindowsLab.exe");
            if (File.Exists(exe))
            {
                var info = FileVersionInfo.GetVersionInfo(exe);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                {
                    var v = info.ProductVersion!;
                    var plus = v.IndexOf('+', StringComparison.Ordinal);
                    return plus > 0 ? v[..plus] : v;
                }

                if (!string.IsNullOrWhiteSpace(info.FileVersion))
                {
                    return info.FileVersion!;
                }
            }
        }
        catch
        {
            // fall through
        }

        return typeof(InstallRegistration).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}

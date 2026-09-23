using System.Diagnostics;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

/// <summary>
/// Hard-blocked: DiagTrack / SysMain / WSearch.
/// Defender family: blocked for generic service ops (D021 — use curated registry tweak instead).
/// </summary>
public static class ProtectedServices
{
    private static readonly HashSet<string> HardBlocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "DiagTrack", "dmwappushservice",
        "SysMain", "WSearch"
    };

    private static readonly HashSet<string> DefenderFamily = new(StringComparer.OrdinalIgnoreCase)
    {
        "WinDefend", "Sense", "WdNisSvc", "WdFilter", "WdBoot"
    };

    public static bool IsHardBlocked(string serviceName) =>
        !string.IsNullOrWhiteSpace(serviceName) && HardBlocked.Contains(serviceName.Trim());

    public static bool IsDefenderFamily(string serviceName) =>
        !string.IsNullOrWhiteSpace(serviceName) && DefenderFamily.Contains(serviceName.Trim());

    /// <summary>True if a generic service op must refuse this name.</summary>
    public static bool IsBlocked(string serviceName) =>
        IsHardBlocked(serviceName) || IsDefenderFamily(serviceName);
}

public static class ServiceOpExecutor
{
    public static int? ReadStartType(string serviceName)
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\" + serviceName);
        return key?.GetValue("Start") as int?;
    }

    public static string CaptureConfig(string serviceName)
    {
        return RunSc($"qc \"{serviceName}\"") + "\n" + RunSc($"qfailure \"{serviceName}\"");
    }

    public static void SetStartType(string serviceName, string desired)
    {
        if (ProtectedServices.IsBlocked(serviceName))
        {
            throw new InvalidOperationException($"Refusing to mutate protected service: {serviceName}");
        }

        var mode = desired.Trim().ToLowerInvariant() switch
        {
            "auto" or "automatic" or "2" => "auto",
            "demand" or "manual" or "3" => "demand",
            "disabled" or "4" => "disabled",
            "boot" or "0" => "boot",
            "system" or "1" => "system",
            _ => throw new InvalidOperationException("Unknown service start type: " + desired)
        };

        var code = RunScExit($"config \"{serviceName}\" start= {mode}");
        if (code != 0)
        {
            throw new InvalidOperationException($"sc config failed ({code}) for {serviceName}");
        }
    }

    public static void RestoreStartType(string serviceName, int previousStartType)
    {
        var map = previousStartType switch
        {
            0 => "boot",
            1 => "system",
            2 => "auto",
            3 => "demand",
            4 => "disabled",
            _ => "demand"
        };
        SetStartType(serviceName, map);
    }

    private static string RunSc(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null)
        {
            return "";
        }

        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(30_000);
        return output;
    }

    private static int RunScExit(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null)
        {
            return -1;
        }

        p.WaitForExit(30_000);
        return p.ExitCode;
    }
}

public static class TaskOpExecutor
{
    public static string? ExportXml(string taskPath)
    {
        var outFile = Path.Combine(Path.GetTempPath(), "wl-task-" + Guid.NewGuid().ToString("N") + ".xml");
        var code = RunSchtasks($"/Query /TN \"{taskPath}\" /XML", outFile);
        if (code != 0 || !File.Exists(outFile))
        {
            return null;
        }

        return outFile;
    }

    public static bool? IsEnabled(string taskPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Query /TN \"{taskPath}\" /FO LIST /V",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null)
        {
            return null;
        }

        var text = p.StandardOutput.ReadToEnd();
        p.WaitForExit(30_000);
        if (text.Contains("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (text.Contains("Ready", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Running", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return null;
    }

    public static void SetEnabled(string taskPath, bool enable)
    {
        var flag = enable ? "/Enable" : "/Disable";
        var code = RunSchtasks($"/Change /TN \"{taskPath}\" {flag}", null);
        if (code != 0)
        {
            throw new InvalidOperationException($"schtasks change failed ({code}) for {taskPath}");
        }
    }

    private static int RunSchtasks(string args, string? redirectXmlTo)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = redirectXmlTo is not null,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null)
        {
            return -1;
        }

        if (redirectXmlTo is not null)
        {
            var xml = p.StandardOutput.ReadToEnd();
            File.WriteAllText(redirectXmlTo, xml);
        }

        p.WaitForExit(60_000);
        return p.ExitCode;
    }
}

public static class PowerOpExecutor
{
    public static string? GetActiveSchemeGuid()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = "/getactivescheme",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null)
        {
            return null;
        }

        var text = p.StandardOutput.ReadToEnd();
        p.WaitForExit(15_000);
        // GUID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        var idx = text.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var slice = text[(idx + 5)..].Trim();
        var end = slice.IndexOfAny([' ', '\r', '\n', '(']);
        return end < 0 ? slice.Trim() : slice[..end].Trim();
    }

    public static string? ExportScheme(string schemeGuid, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, schemeGuid + ".pow");
        var code = RunPowerCfg($"/export \"{path}\" {schemeGuid}");
        return code == 0 && File.Exists(path) ? path : null;
    }

    public static void SetActiveScheme(string schemeGuid)
    {
        var code = RunPowerCfg($"/setactive {schemeGuid}");
        if (code != 0)
        {
            throw new InvalidOperationException($"powercfg /setactive failed ({code})");
        }
    }

    private static int RunPowerCfg(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null)
        {
            return -1;
        }

        p.WaitForExit(30_000);
        return p.ExitCode;
    }
}

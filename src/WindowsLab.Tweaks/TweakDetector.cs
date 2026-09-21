using Microsoft.Win32;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public interface IRegistryReader
{
    object? GetValue(string hive, string path, string name);
}

public sealed class LiveRegistryReader : IRegistryReader
{
    public object? GetValue(string hive, string path, string name)
    {
        using var key = Open(hive, path);
        return key?.GetValue(name);
    }

    private static RegistryKey? Open(string hive, string path)
    {
        var root = hive.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKU" or "HKEY_USERS" => Registry.Users,
            _ => null
        };

        return root?.OpenSubKey(path);
    }
}

public static class TweakDetector
{
    public static TweakDetection Detect(TweakDefinition tweak, IRegistryReader registry)
    {
        ArgumentNullException.ThrowIfNull(tweak);
        ArgumentNullException.ThrowIfNull(registry);

        var primary = tweak.Ops.Count > 0 ? tweak.Ops[0] : null;
        var kind = primary?.Kind?.ToLowerInvariant() ?? "registry";

        try
        {
            if (kind == "service" && !string.IsNullOrWhiteSpace(primary?.Name))
            {
                var start = ServiceOpExecutor.ReadStartType(primary!.Name!);
                var desired = MapServiceDesired(primary.Value?.ToString() ?? tweak.DesiredEquals);
                var actual = start?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(missing)";
                var match = start is int s && s == desired;
                return new TweakDetection(tweak.Id, ProbeStatus.Ok, actual, desired.ToString(System.Globalization.CultureInfo.InvariantCulture), match, null);
            }

            if (kind == "power")
            {
                var guid = PowerOpExecutor.GetActiveSchemeGuid();
                var desired = (primary?.Value?.ToString() ?? tweak.DesiredEquals)?.Trim() ?? "";
                var match = string.Equals(guid, desired, StringComparison.OrdinalIgnoreCase);
                return new TweakDetection(tweak.Id, ProbeStatus.Ok, guid ?? "(missing)", desired, match, null);
            }

            if (kind == "task")
            {
                var path = primary?.Path ?? primary?.Name ?? "";
                var enabled = TaskOpExecutor.IsEnabled(path);
                var wantDisable = string.Equals(primary?.Value?.ToString(), "disable", StringComparison.OrdinalIgnoreCase)
                                  || tweak.DesiredEquals == "0";
                var match = enabled is bool e && (wantDisable ? !e : e);
                return new TweakDetection(
                    tweak.Id,
                    ProbeStatus.Ok,
                    enabled is null ? "(unknown)" : (enabled.Value ? "enabled" : "disabled"),
                    wantDisable ? "disabled" : "enabled",
                    match,
                    null);
            }

            var raw = registry.GetValue(tweak.Detect.Hive, tweak.Detect.Path, tweak.Detect.Name);
            var actualReg = Format(raw);
            var desiredReg = FormatDesired(tweak.DesiredEquals);
            var matchReg = ValuesMatch(raw, tweak.DesiredEquals);
            return new TweakDetection(tweak.Id, ProbeStatus.Ok, actualReg, desiredReg, matchReg, raw is null ? "Value missing" : null);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new TweakDetection(tweak.Id, ProbeStatus.Denied, null, FormatDesired(tweak.DesiredEquals), false, ex.Message);
        }
        catch (Exception ex)
        {
            return new TweakDetection(tweak.Id, ProbeStatus.Error, null, FormatDesired(tweak.DesiredEquals), false, ex.Message);
        }
    }

    public static IReadOnlyList<TweakDetection> DetectAll(IEnumerable<TweakDefinition> tweaks, IRegistryReader registry) =>
        tweaks.Select(t => Detect(t, registry)).ToArray();

    private static int MapServiceDesired(string desired) => desired.Trim().ToLowerInvariant() switch
    {
        "boot" or "0" => 0,
        "system" or "1" => 1,
        "auto" or "automatic" or "2" => 2,
        "demand" or "manual" or "3" => 3,
        "disabled" or "4" => 4,
        _ => int.TryParse(desired, out var n) ? n : 4
    };

    private static bool ValuesMatch(object? actual, string? desired)
    {
        if (desired is null)
        {
            return actual is null;
        }

        if (actual is null)
        {
            return false;
        }

        return string.Equals(Format(actual), desired, StringComparison.OrdinalIgnoreCase);
    }

    private static string Format(object? value) => value switch
    {
        null => "(missing)",
        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
        byte[] bytes => Convert.ToHexString(bytes),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };

    private static string? FormatDesired(string? desired) => desired;
}

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

        try
        {
            var raw = registry.GetValue(tweak.Detect.Hive, tweak.Detect.Path, tweak.Detect.Name);
            var actual = Format(raw);
            var desired = FormatDesired(tweak.DesiredEquals);
            var match = ValuesMatch(raw, tweak.DesiredEquals);
            return new TweakDetection(tweak.Id, ProbeStatus.Ok, actual, desired, match, raw is null ? "Value missing" : null);
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

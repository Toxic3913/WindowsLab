using Microsoft.Win32;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public interface IRegistryWriter
{
    void SetValue(string hive, string path, string name, object value, RegistryValueKind kind);

    void DeleteValue(string hive, string path, string name);
}

public sealed class LiveRegistryWriter : IRegistryWriter
{
    public void SetValue(string hive, string path, string name, object value, RegistryValueKind kind)
    {
        EnsureHkcu(hive);
        using var key = OpenWritable(hive, path, create: true)
                        ?? throw new InvalidOperationException($"Cannot open registry key {hive}\\{path}");
        key.SetValue(name, value, kind);
    }

    public void DeleteValue(string hive, string path, string name)
    {
        EnsureHkcu(hive);
        using var key = OpenWritable(hive, path, create: false);
        if (key is null)
        {
            return;
        }

        try
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (ArgumentException)
        {
            // missing value — ok
        }
    }

    private static void EnsureHkcu(string hive)
    {
        var ok = hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
                 || hive.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
        if (!ok)
        {
            throw new InvalidOperationException("Beta 0.1 lab writer refuses non-HKCU hives: " + hive);
        }
    }

    private static RegistryKey? OpenWritable(string hive, string path, bool create)
    {
        var root = hive.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKU" or "HKEY_USERS" => Registry.Users,
            _ => null
        };

        if (root is null)
        {
            return null;
        }

        return create ? root.CreateSubKey(path, writable: true) : root.OpenSubKey(path, writable: true);
    }
}

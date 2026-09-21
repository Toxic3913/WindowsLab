using Microsoft.Win32;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public interface IRegistryWriter
{
    void SetValue(string hive, string path, string name, object value, RegistryValueKind kind);

    void DeleteValue(string hive, string path, string name);
}

/// <summary>Unelevated lab writer — HKCU only (D018).</summary>
public sealed class LiveRegistryWriter : IRegistryWriter
{
    private readonly bool _allowMachineHive;

    public LiveRegistryWriter(bool allowMachineHive = false)
    {
        _allowMachineHive = allowMachineHive;
    }

    public void SetValue(string hive, string path, string name, object value, RegistryValueKind kind)
    {
        EnsureAllowed(hive);
        using var key = OpenWritable(hive, path, create: true)
                        ?? throw new InvalidOperationException($"Cannot open registry key {hive}\\{path}");
        key.SetValue(name, value, kind);
    }

    public void DeleteValue(string hive, string path, string name)
    {
        EnsureAllowed(hive);
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

    private void EnsureAllowed(string hive)
    {
        if (IsHkcu(hive))
        {
            return;
        }

        if (_allowMachineHive && IsHklm(hive))
        {
            return;
        }

        throw new InvalidOperationException(
            _allowMachineHive
                ? "Unsupported hive: " + hive
                : "Unelevated writer refuses non-HKCU hives: " + hive);
    }

    private static bool IsHkcu(string hive) =>
        hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
        || hive.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);

    private static bool IsHklm(string hive) =>
        hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase)
        || hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase);

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

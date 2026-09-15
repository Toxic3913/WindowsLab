using Microsoft.Win32;

namespace WindowsLab.Tweaks;

/// <summary>Records intended writes without touching the registry.</summary>
public sealed class DryRunRegistryWriter : IRegistryWriter
{
    public IList<string> Operations { get; } = new List<string>();

    public void SetValue(string hive, string path, string name, object value, RegistryValueKind kind)
    {
        Operations.Add($"SET {hive}\\{path}\\{name}={value} ({kind})");
    }

    public void DeleteValue(string hive, string path, string name)
    {
        Operations.Add($"DEL {hive}\\{path}\\{name}");
    }
}

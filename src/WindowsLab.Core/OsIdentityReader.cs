using Microsoft.Win32;

namespace WindowsLab.Core;

public static class OsIdentityReader
{
    public const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    public const int FallbackMajorVersion = 10;
    public const int FallbackMinorVersion = 0;

    public static OsIdentity Read()
    {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey)
            ?? throw new InvalidOperationException("HKLM CurrentVersion registry key is missing.");

        var buildValue = key.GetValue("CurrentBuild") as string
            ?? key.GetValue("CurrentBuildNumber")?.ToString();

        var facts = OsIdentityFactsParser.Parse(
            currentBuild: buildValue,
            major: ReadInt32(key, "CurrentMajorVersionNumber", FallbackMajorVersion),
            minor: ReadInt32(key, "CurrentMinorVersionNumber", FallbackMinorVersion),
            ubr: ReadInt32(key, "UBR", 0),
            displayVersion: key.GetValue("DisplayVersion") as string,
            editionId: key.GetValue("EditionID") as string,
            productName: key.GetValue("ProductName") as string,
            compositionEditionId: key.GetValue("CompositionEditionID") as string);

        return OsIdentityMapper.Map(facts);
    }

    private static int ReadInt32(RegistryKey key, string name, int fallback)
    {
        return key.GetValue(name) is int value ? value : fallback;
    }
}

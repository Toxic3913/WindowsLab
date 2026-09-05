namespace WindowsLab.Core;

/// <summary>
/// OS family from CurrentBuild. ProductName is diagnostic-only (Windows 11 25H2 often still says "Windows 10").
/// </summary>
public static class OsIdentityMapper
{
    public const int Windows11MinBuild = 22000;
    public const int Windows10MinBuild = 10240;

    public static bool IsWindows11(int currentBuildNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentBuildNumber);
        return currentBuildNumber >= Windows11MinBuild;
    }

    public static string FamilyLabel(int currentBuildNumber)
    {
        if (IsWindows11(currentBuildNumber))
        {
            return "Windows 11";
        }

        if (currentBuildNumber >= Windows10MinBuild)
        {
            return "Windows 10";
        }

        return "Windows";
    }

    public static OsIdentity Map(OsIdentityFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var isWindows11 = IsWindows11(facts.Build);
        string? warning = null;
        if (isWindows11 && facts.ProductName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            warning = "ProductName reports Windows 10; OS family is Windows 11 because CurrentBuild >= 22000.";
        }

        return new OsIdentity(
            facts.Major,
            facts.Minor,
            facts.Build,
            facts.Ubr,
            facts.DisplayVersion,
            facts.EditionId,
            facts.ProductName,
            facts.CompositionEditionId,
            isWindows11,
            FamilyLabel(facts.Build),
            warning);
    }
}

namespace WindowsLab.Core;

public static class OsIdentityFactsParser
{
    public static OsIdentityFacts Parse(
        string? currentBuild,
        int major,
        int minor,
        int ubr,
        string? displayVersion,
        string? editionId,
        string? productName,
        string? compositionEditionId = null)
    {
        if (!int.TryParse(currentBuild, out var build))
        {
            throw new FormatException("CurrentBuild is missing or not an integer.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(build);

        return new OsIdentityFacts(
            major,
            minor,
            build,
            ubr,
            displayVersion ?? string.Empty,
            editionId ?? string.Empty,
            productName ?? string.Empty,
            string.IsNullOrWhiteSpace(compositionEditionId) ? null : compositionEditionId);
    }
}

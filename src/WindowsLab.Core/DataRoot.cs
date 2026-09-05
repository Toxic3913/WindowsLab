namespace WindowsLab.Core;

public enum DataRootKind
{
    ProgramData
}

public sealed record DataRoot(string Path, DataRootKind Kind);

/// <summary>
/// Runtime artifacts (backups/logs/reports) on the system drive.
/// Never the git repo at D:\WindowsLab.
/// </summary>
public static class DataRootResolver
{
    public const string FolderName = "WindowsLab";

    public static DataRoot Resolve(string programDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programDataDirectory);

        return new DataRoot(
            Path.Combine(programDataDirectory, FolderName),
            DataRootKind.ProgramData);
    }
}

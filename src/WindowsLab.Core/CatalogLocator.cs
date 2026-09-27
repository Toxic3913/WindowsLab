namespace WindowsLab.Core;

public static class CatalogLocator
{
    public static string? FindTweaksDirectory(string? startDirectory = null) =>
        FindCatalogSubdirectory("tweaks", startDirectory);

    public static string? FindChecklistsDirectory(string? startDirectory = null) =>
        FindCatalogSubdirectory("checklists", startDirectory);

    public static string? FindPresetsDirectory(string? startDirectory = null) =>
        FindCatalogSubdirectory("presets", startDirectory);

    public static string? FindApplicationsDirectory(string? startDirectory = null) =>
        FindCatalogSubdirectory("applications", startDirectory);

    public static string? FindWorkloadsDirectory(string? startDirectory = null) =>
        FindCatalogSubdirectory("workloads", startDirectory);

    private static string? FindCatalogSubdirectory(string name, string? startDirectory)
    {
        var current = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "catalog", name);
            if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*.json").Any())
            {
                return candidate;
            }

            current = current.Parent;
        }

        var nextToApp = Path.Combine(AppContext.BaseDirectory, "catalog", name);
        return Directory.Exists(nextToApp) ? nextToApp : null;
    }
}

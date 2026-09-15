using WindowsLab.Tweaks;

namespace WindowsLab.Core.Tests;

public sealed class TweakCatalogLoaderTests
{
    [Fact]
    public void Loads_repo_catalog_with_at_least_thirty_tweaks()
    {
        var dir = FindCatalog();
        var catalog = TweakCatalogLoader.LoadDirectory(dir);
        Assert.True(catalog.Count >= 30, $"expected >= 30 tweaks, got {catalog.Count}");
        Assert.Contains(catalog, t => t.Id == "explorer.show-file-extensions");
        Assert.DoesNotContain(catalog, t => t.Ops.Any(o =>
            string.Equals(o.Kind, "InvokeScript", StringComparison.OrdinalIgnoreCase)));
    }

    private static string FindCatalog()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "catalog", "tweaks");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("catalog/tweaks");
    }
}

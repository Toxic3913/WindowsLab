using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class ToolHubCatalogLoaderTests
{
    [Fact]
    public void Loads_recommended_hub_entries()
    {
        var dir = CatalogLocator.FindToolHubDirectory();
        Assert.NotNull(dir);
        var entries = ToolHubCatalogLoader.LoadDirectory(dir!);
        Assert.Contains(entries, e => e.Id == "hub.desktop.powertoys");
        Assert.Contains(entries, e => e.Id == "hub.desktop.treesize-free");
        Assert.Contains(entries, e => e.Id == "hub.performance.quickcpu");
        Assert.Contains(entries, e => e.Id == "hub.performance.lossless-scaling" && e.Kind == ToolHubKind.Steam);
        Assert.Contains(entries, e => e.Id == "hub.performance.scskiller");
        Assert.Contains(entries, e => e.Id == "hub.network.tcp-optimizer" && e.Kind == ToolHubKind.ExternalUrl);
        Assert.Contains(entries, e => e.Id == "hub.accessibility.filter-keys" && e.Kind == ToolHubKind.WindowsSettings);
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Title)));
    }
}

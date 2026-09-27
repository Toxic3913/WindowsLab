using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class ExternalWorkloadTests
{
    [Fact]
    public void Catalog_loads_from_repo_workloads_dir()
    {
        var dir = CatalogLocator.FindWorkloadsDirectory();
        Assert.NotNull(dir);
        var catalog = ExternalWorkloadCatalog.LoadDirectory(dir!);
        Assert.Contains(catalog, w => w.Id == "workload.steam");
        Assert.Contains(catalog, w => w.Id == "workload.overwolf");
        Assert.Contains(catalog, w => w.Id == "workload.riot");
        Assert.All(catalog, w => Assert.StartsWith("workload.", w.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Detect_steam_does_not_throw()
    {
        var def = new ExternalWorkloadDefinition(
            "workload.steam",
            "Steam",
            "test",
            RiskLevel.Low,
            ["steam", "steamwebhelper"],
            ["Steam Client Service"]);
        var status = ExternalWorkloadController.Detect(def);
        Assert.Equal("workload.steam", status.Id);
        Assert.False(string.IsNullOrWhiteSpace(status.Summary));
    }
}

using WindowsLab.Audit;

namespace WindowsLab.Core.Tests;

public sealed class AuditRunnerTests
{
    [Fact]
    public void Run_returns_os_without_using_product_name_for_family()
    {
        var inventory = AuditRunner.Run();
        Assert.True(inventory.Os.Build > 0);
        Assert.Contains(inventory.Probes, p => p.ProbeId == "os.identity");
        if (inventory.Os.Build >= 22000)
        {
            Assert.True(inventory.Os.IsWindows11);
        }
    }
}

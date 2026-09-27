using System.Reflection;
using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class ProductVersionTests
{
    [Fact]
    public void Informational_version_is_1_2_2()
    {
        var v = typeof(Loc).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        Assert.StartsWith("1.2.2", v ?? "", StringComparison.Ordinal);
        Assert.StartsWith("1.2.2", AppUpdateChecker.GetCurrentVersion(), StringComparison.Ordinal);
    }
}

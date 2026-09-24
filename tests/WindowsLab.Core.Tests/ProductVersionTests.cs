using System.Reflection;
using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class ProductVersionTests
{
    [Fact]
    public void Informational_version_is_1_1_0()
    {
        var v = typeof(Loc).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        Assert.StartsWith("1.1.0", v ?? "", StringComparison.Ordinal);
        Assert.StartsWith("1.1.0", AppUpdateChecker.GetCurrentVersion(), StringComparison.Ordinal);
    }
}

using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class DataRootResolverTests
{
    [Fact]
    public void Uses_programdata_on_the_system_drive()
    {
        var root = DataRootResolver.Resolve(@"C:\ProgramData");

        Assert.Equal(@"C:\ProgramData\WindowsLab", root.Path);
        Assert.Equal(DataRootKind.ProgramData, root.Kind);
    }

    [Fact]
    public void Never_uses_the_source_repo_or_D_as_runtime_root()
    {
        var root = DataRootResolver.Resolve(@"C:\ProgramData");

        Assert.NotEqual(@"D:\WindowsLab", root.Path);
        Assert.DoesNotContain(@"D:\", root.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_programdata_throws()
    {
        Assert.Throws<ArgumentException>(() => DataRootResolver.Resolve(""));
    }
}

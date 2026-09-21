using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class AppUpdateCheckerTests
{
    [Theory]
    [InlineData("0.3.0", "0.3.0", false)]
    [InlineData("v0.3.0", "0.3.0", false)]
    [InlineData("0.4.0", "0.3.0", true)]
    [InlineData("v0.2.9", "0.3.0", false)]
    [InlineData("0.3.0-beta", "0.2.0", true)]
    public void IsNewer_compares_semver_tags(string remote, string local, bool expected)
    {
        Assert.Equal(expected, AppUpdateChecker.IsNewer(remote, local));
    }

    [Fact]
    public void GetCurrentVersion_is_non_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppUpdateChecker.GetCurrentVersion()));
    }
}

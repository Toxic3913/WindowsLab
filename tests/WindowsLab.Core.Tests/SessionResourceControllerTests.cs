using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class SessionResourceControllerTests
{
    [Fact]
    public void Hard_blocks_defender_and_sysmain()
    {
        Assert.True(ExternalWorkloadController.IsProcessHardBlocked("MsMpEng"));
        Assert.True(ExternalWorkloadController.IsServiceHardBlocked("SysMain"));
        Assert.True(ExternalWorkloadController.IsServiceHardBlocked("WSearch"));
        Assert.False(ExternalWorkloadController.IsProcessHardBlocked("notepad"));
    }

    [Fact]
    public void ListProcesses_marks_blocked_rows()
    {
        var rows = SessionResourceController.ListProcesses(50);
        Assert.NotEmpty(rows);
        Assert.All(rows.Where(r => ExternalWorkloadController.IsProcessHardBlocked(r.Name)), r => Assert.True(r.IsBlocked));
    }

    [Fact]
    public void Network_sample_returns_structure()
    {
        var net = NetworkTrafficSampler.Sample();
        Assert.NotNull(net.Adapters);
        Assert.True(net.TotalBytesReceivedPerSec >= 0);
        Assert.True(net.TotalBytesSentPerSec >= 0);
    }

    [Theory]
    [InlineData("NordVPN", null, null, ProcessGroup.External, true)]
    [InlineData("ollama", null, null, ProcessGroup.External, true)]
    [InlineData("LuaTools", null, null, ProcessGroup.External, true)]
    [InlineData("steamwebhelper", null, null, ProcessGroup.External, true)]
    [InlineData("notepad", @"C:\Windows\System32\notepad.exe", null, ProcessGroup.Windows, false)]
    public void Background_interest_detects_vpn_steam_ollama(
        string name, string? path, string? title, ProcessGroup group, bool expected)
    {
        Assert.Equal(expected, SessionResourceController.IsBackgroundInterest(name, path, title, group));
    }

    [Fact]
    public void ListServices_includes_running_entries()
    {
        var svcs = SessionResourceController.ListServices(400);
        Assert.NotEmpty(svcs);
        Assert.Contains(svcs, s => s.IsRunning);
    }
}

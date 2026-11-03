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
}

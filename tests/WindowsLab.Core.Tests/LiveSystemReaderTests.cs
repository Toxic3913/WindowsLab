using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class LiveSystemReaderTests
{
    [Fact]
    public void Read_returns_machine_and_disk_lines()
    {
        var snap = LiveSystemReader.Read();
        Assert.False(string.IsNullOrWhiteSpace(snap.ComputerName));
        Assert.False(string.IsNullOrWhiteSpace(snap.OsLine));
        Assert.Contains("C:", snap.DiskLine, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(snap.CpuPercent, 0, 100);
        Assert.InRange(snap.RamPercent, 0, 100);
    }
}

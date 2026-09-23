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

    [Fact]
    public void ReadDashboard_includes_fixed_disks_and_ram_available()
    {
        var dash = LiveSystemReader.ReadDashboard(includeProcesses: false);
        Assert.False(string.IsNullOrWhiteSpace(dash.ComputerName));
        Assert.NotEmpty(dash.Disks);
        Assert.Contains(dash.Disks, d => d.Root.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
        Assert.True(dash.Ram.TotalGb > 0);
        Assert.True(dash.Ram.AvailableGb >= 0);
        Assert.InRange(dash.Ram.Percent, 0, 100);
        Assert.Null(dash.Processes);
    }

    [Fact]
    public void ReadDashboard_with_processes_returns_groups()
    {
        ProcessSampler.ResetCpuHistoryForTests();
        _ = ProcessSampler.Sample(5);
        var dash = LiveSystemReader.ReadDashboard(includeProcesses: true, processTopN: 10);
        Assert.NotNull(dash.Processes);
        Assert.NotEmpty(dash.Processes!.TopByWorkingSet);
        Assert.Equal(3, dash.Processes.GroupTotals.Count);
    }
}

public sealed class ProcessSamplerTests
{
    [Theory]
    [InlineData(0, "System", null, ProcessGroup.Windows)]
    [InlineData(4, "System", null, ProcessGroup.Windows)]
    [InlineData(100, "svchost", null, ProcessGroup.Windows)]
    [InlineData(200, "explorer", @"C:\Windows\explorer.exe", ProcessGroup.Windows)]
    [InlineData(201, "notepad", @"C:\Windows\System32\notepad.exe", ProcessGroup.Windows)]
    [InlineData(300, "Store", @"C:\Program Files\WindowsApps\Microsoft.WindowsStore\Store.exe", ProcessGroup.Microsoft)]
    [InlineData(301, "OUTLOOK", @"C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE", ProcessGroup.Microsoft)]
    [InlineData(400, "chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", ProcessGroup.External)]
    [InlineData(401, "steam", @"D:\Steam\steam.exe", ProcessGroup.External)]
    [InlineData(402, "mystery", null, ProcessGroup.External)]
    public void Classify_path_heuristics(int pid, string name, string? path, ProcessGroup expected)
    {
        Assert.Equal(expected, ProcessSampler.Classify(pid, name, path));
    }

    [Fact]
    public void Sample_returns_top_n_and_all_group_totals()
    {
        ProcessSampler.ResetCpuHistoryForTests();
        var first = ProcessSampler.Sample(topN: 5);
        Assert.True(first.TopByWorkingSet.Count <= 5);
        Assert.Equal(3, first.GroupTotals.Count);
        Assert.Contains(first.GroupTotals, g => g.Group == ProcessGroup.Windows);

        // Second sample enables CPU deltas (elapsed > 0 via Math.Max(0.001, …)).
        var second = ProcessSampler.Sample(topN: 5);
        Assert.True(second.TopByCpu.Count <= 5);
        Assert.Equal(3, second.GroupTotals.Count);
    }
}

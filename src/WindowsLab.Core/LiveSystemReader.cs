using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace WindowsLab.Core;

/// <summary>
/// Lightweight live snapshot for DesktopInfo / BGInfo-style overlays.
/// Does not use DXGI or heavy WMI (safe on VMware guests).
/// </summary>
public sealed record LiveSystemSnapshot(
    DateTimeOffset CapturedUtc,
    string ComputerName,
    string UserName,
    string OsLine,
    string CpuLine,
    double CpuPercent,
    string RamLine,
    double RamPercent,
    string DiskLine,
    string NetworkLine);

public sealed record LiveDiskSnapshot(
    string Root,
    double FreeGb,
    double TotalGb,
    double FreePercent);

public sealed record LiveRamSnapshot(
    double UsedGb,
    double AvailableGb,
    double TotalGb,
    double Percent,
    double? CommitUsedGb,
    double? CommitLimitGb,
    double? CommitPercent);

public sealed record LiveDiskIoSnapshot(
    double? PercentDiskTime,
    double? AvgQueueLength);

/// <summary>Structured live metrics for the Performance dashboard (D022).</summary>
public sealed record LiveDashboardSnapshot(
    DateTimeOffset CapturedUtc,
    string ComputerName,
    string UserName,
    string OsLine,
    double CpuPercent,
    LiveRamSnapshot Ram,
    IReadOnlyList<LiveDiskSnapshot> Disks,
    LiveDiskIoSnapshot DiskIo,
    string NetworkLine,
    ProcessSampleResult? Processes);

public static class LiveSystemReader
{
    private static readonly object InitLock = new();
    private static PerformanceCounter? _cpu;
    private static PerformanceCounter? _commitLimit;
    private static PerformanceCounter? _commitBytes;
    private static PerformanceCounter? _diskTime;
    private static PerformanceCounter? _diskQueue;
    private static bool _diskCountersProbed;
    private static bool _diskCountersAvailable;

    public static LiveSystemSnapshot Read()
    {
        var dash = ReadDashboard(includeProcesses: false);
        var c = dash.Disks.FirstOrDefault(d =>
            d.Root.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
        var diskLine = c is null
            ? (dash.Disks.Count == 0 ? "C: unknown" : FormatDisk(dash.Disks[0]))
            : FormatDisk(c);

        return new LiveSystemSnapshot(
            dash.CapturedUtc,
            dash.ComputerName,
            dash.UserName,
            dash.OsLine,
            $"{Environment.ProcessorCount} logical processors · {dash.CpuPercent:0.0}%",
            dash.CpuPercent,
            $"{dash.Ram.UsedGb:0.0} / {dash.Ram.TotalGb:0.0} GB ({dash.Ram.Percent:0.0}%)",
            dash.Ram.Percent,
            diskLine,
            dash.NetworkLine);
    }

    /// <summary>
    /// Full dashboard snapshot. CPU uses a non-blocking counter sample (no Thread.Sleep).
    /// Call on a background thread or via timer; first sample after process start may read ~0.
    /// </summary>
    public static LiveDashboardSnapshot ReadDashboard(bool includeProcesses = true, int processTopN = 25)
    {
        var os = TryOsLine();
        var cpuPct = ReadCpuPercent();
        var ram = ReadRamDetailed();
        var disks = ReadFixedDisks();
        var diskIo = ReadDiskIo();
        var net = ReadNetwork();
        ProcessSampleResult? procs = includeProcesses
            ? ProcessSampler.Sample(processTopN)
            : null;

        return new LiveDashboardSnapshot(
            DateTimeOffset.Now,
            Environment.MachineName,
            Environment.UserName,
            os,
            cpuPct,
            ram,
            disks,
            diskIo,
            net,
            procs);
    }

    private static string FormatDisk(LiveDiskSnapshot d) =>
        $"{d.Root} {d.FreeGb:0.0} GB free / {d.TotalGb:0.0} GB";

    private static string TryOsLine()
    {
        try
        {
            var id = OsIdentityReader.Read();
            return $"{id.FamilyLabel} {id.DisplayVersion} build {id.Build}.{id.Ubr} ({id.EditionId})";
        }
        catch
        {
            return Environment.OSVersion.VersionString;
        }
    }

    /// <summary>Non-blocking CPU %. Avoids Thread.Sleep so UI timers stay responsive.</summary>
    private static double ReadCpuPercent()
    {
        try
        {
            EnsureCpuCounter();
            return _cpu is null ? 0 : Math.Clamp(_cpu.NextValue(), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static void EnsureCpuCounter()
    {
        if (_cpu is not null)
        {
            return;
        }

        lock (InitLock)
        {
            _cpu ??= new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
        }
    }

    private static LiveRamSnapshot ReadRamDetailed()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            return new LiveRamSnapshot(0, 0, 0, 0, null, null, null);
        }

        var total = status.TotalPhys / (1024d * 1024 * 1024);
        var avail = status.AvailPhys / (1024d * 1024 * 1024);
        var used = total - avail;
        var pct = 100d * used / total;

        double? commitUsed = null;
        double? commitLimit = null;
        double? commitPct = null;
        try
        {
            EnsureCommitCounters();
            if (_commitBytes is not null && _commitLimit is not null)
            {
                var usedB = _commitBytes.NextValue();
                var limitB = _commitLimit.NextValue();
                if (limitB > 0)
                {
                    commitUsed = usedB / (1024d * 1024 * 1024);
                    commitLimit = limitB / (1024d * 1024 * 1024);
                    commitPct = 100d * usedB / limitB;
                }
            }
        }
        catch
        {
            // best-effort; omit rather than invent
        }

        return new LiveRamSnapshot(used, avail, total, pct, commitUsed, commitLimit, commitPct);
    }

    private static void EnsureCommitCounters()
    {
        if (_commitBytes is not null && _commitLimit is not null)
        {
            return;
        }

        lock (InitLock)
        {
            _commitBytes ??= new PerformanceCounter("Memory", "Committed Bytes", true);
            _commitLimit ??= new PerformanceCounter("Memory", "Commit Limit", true);
        }
    }

    private static LiveDiskSnapshot[] ReadFixedDisks()
    {
        var list = new List<LiveDiskSnapshot>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady)
                {
                    continue;
                }

                try
                {
                    var total = d.TotalSize / (1024d * 1024 * 1024);
                    if (total <= 0)
                    {
                        continue;
                    }

                    var free = d.AvailableFreeSpace / (1024d * 1024 * 1024);
                    var freePct = 100d * free / total;
                    var root = d.Name.TrimEnd('\\');
                    if (!root.EndsWith(':'))
                    {
                        root += ":";
                    }

                    list.Add(new LiveDiskSnapshot(root, free, total, freePct));
                }
                catch
                {
                    // skip unreadable volume
                }
            }
        }
        catch
        {
            // empty list — never invent
        }

        return list
            .OrderBy(x => x.Root, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static LiveDiskIoSnapshot ReadDiskIo()
    {
        EnsureDiskCounters();
        if (!_diskCountersAvailable || _diskTime is null || _diskQueue is null)
        {
            return new LiveDiskIoSnapshot(null, null);
        }

        try
        {
            return new LiveDiskIoSnapshot(
                Math.Clamp(_diskTime.NextValue(), 0, 100),
                Math.Max(0, _diskQueue.NextValue()));
        }
        catch
        {
            return new LiveDiskIoSnapshot(null, null);
        }
    }

    private static void EnsureDiskCounters()
    {
        if (_diskCountersProbed)
        {
            return;
        }

        lock (InitLock)
        {
            if (_diskCountersProbed)
            {
                return;
            }

            try
            {
                _diskTime = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
                _diskQueue = new PerformanceCounter("PhysicalDisk", "Avg. Disk Queue Length", "_Total", true);
                _ = _diskTime.NextValue();
                _ = _diskQueue.NextValue();
                _diskCountersAvailable = true;
            }
            catch
            {
                _diskCountersAvailable = false;
                _diskTime = null;
                _diskQueue = null;
            }

            _diskCountersProbed = true;
        }
    }

    private static string ReadNetwork()
    {
        try
        {
            var ips = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .Distinct()
                .Take(3)
                .ToArray();
            return ips.Length == 0 ? string.Empty : string.Join(", ", ips);
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}

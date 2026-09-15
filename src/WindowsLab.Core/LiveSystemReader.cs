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

public static class LiveSystemReader
{
    private static PerformanceCounter? _cpu;

    public static LiveSystemSnapshot Read()
    {
        var os = TryOsLine();
        var cpuPct = ReadCpuPercent();
        var (ramUsedGb, ramTotalGb, ramPct) = ReadRam();
        var disk = ReadDisk();
        var net = ReadNetwork();

        return new LiveSystemSnapshot(
            DateTimeOffset.Now,
            Environment.MachineName,
            Environment.UserName,
            os,
            $"{Environment.ProcessorCount} logical processors · {cpuPct:0.0}%",
            cpuPct,
            $"{ramUsedGb:0.0} / {ramTotalGb:0.0} GB ({ramPct:0.0}%)",
            ramPct,
            disk,
            net);
    }

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

    private static double ReadCpuPercent()
    {
        try
        {
            _cpu ??= new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _ = _cpu.NextValue();
            Thread.Sleep(80);
            return Math.Clamp(_cpu.NextValue(), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static (double UsedGb, double TotalGb, double Percent) ReadRam()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            return (0, 0, 0);
        }

        var total = status.TotalPhys / (1024d * 1024 * 1024);
        var avail = status.AvailPhys / (1024d * 1024 * 1024);
        var used = total - avail;
        var pct = 100d * used / total;
        return (used, total, pct);
    }

    private static string ReadDisk()
    {
        try
        {
            var d = new DriveInfo("C");
            if (!d.IsReady)
            {
                return "C: not ready";
            }

            var free = d.AvailableFreeSpace / (1024d * 1024 * 1024);
            var total = d.TotalSize / (1024d * 1024 * 1024);
            return $"C: {free:0.0} GB free / {total:0.0} GB";
        }
        catch
        {
            return "C: unknown";
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
            return ips.Length == 0 ? "(sin IPv4)" : string.Join(", ", ips);
        }
        catch
        {
            return "(red unknown)";
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

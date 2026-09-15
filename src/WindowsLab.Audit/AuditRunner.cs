using System.Management;
using WindowsLab.Core;

namespace WindowsLab.Audit;

internal static class Wmi
{
    public static ManagementObjectCollection? Query(string query, string scope = @"root\cimv2")
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            return searcher.Get();
        }
        catch (ManagementException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    public static string String(ManagementBaseObject obj, string property) =>
        obj[property]?.ToString()?.Trim() ?? string.Empty;

    public static uint UInt(ManagementBaseObject obj, string property)
    {
        return obj[property] switch
        {
            uint u => u,
            int i when i >= 0 => (uint)i,
            ushort s => s,
            _ => 0
        };
    }

    public static ulong ULong(ManagementBaseObject obj, string property)
    {
        return obj[property] switch
        {
            ulong u => u,
            uint u => u,
            long l when l >= 0 => (ulong)l,
            int i when i >= 0 => (uint)i,
            _ => 0
        };
    }

    public static bool? Bool(ManagementBaseObject obj, string property) =>
        obj[property] is bool b ? b : null;

    public static int? Int(ManagementBaseObject obj, string property) =>
        obj[property] switch
        {
            int i => i,
            uint u when u <= int.MaxValue => (int)u,
            _ => null
        };
}

public static class AuditRunner
{
    public static MachineInventory Run()
    {
        var probes = new List<ProbeResult>();
        var os = ReadOs(probes);
        var cpu = ReadCpu(probes);
        var gpus = ReadGpus(probes);
        var ram = ReadRam(probes);
        var volumes = ReadVolumes(probes);
        var board = ReadBoard(probes);
        var security = ReadSecurity(probes);
        var tools = ReadToolchain(probes);

        return new MachineInventory(
            DateTimeOffset.UtcNow,
            os,
            cpu,
            gpus,
            ram,
            volumes,
            board,
            security,
            tools,
            probes);
    }

    private static OsIdentity ReadOs(List<ProbeResult> probes)
    {
        try
        {
            var os = OsIdentityReader.Read();
            probes.Add(Ok("os.identity", "windows", os.FamilyLabel, "HKLM CurrentVersion"));
            return os;
        }
        catch (Exception ex)
        {
            probes.Add(Err("os.identity", "windows", ex.Message));
            return OsIdentityMapper.Map(new OsIdentityFacts(10, 0, 0, 0, "", "", "", null));
        }
    }

    private static CpuInventory ReadCpu(List<ProbeResult> probes)
    {
        try
        {
            using var results = Wmi.Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            if (results is null)
            {
                probes.Add(Denied("cpu.wmi", "hardware", "WMI Win32_Processor unavailable"));
                return new CpuInventory("unknown", 0, 0, 0, ProbeStatus.Denied);
            }

            foreach (var obj in results)
            {
                using (obj)
                {
                    var inv = new CpuInventory(
                        Wmi.String(obj, "Name"),
                        (int)Wmi.UInt(obj, "NumberOfCores"),
                        (int)Wmi.UInt(obj, "NumberOfLogicalProcessors"),
                        Wmi.UInt(obj, "MaxClockSpeed"),
                        ProbeStatus.Ok);
                    probes.Add(Ok("cpu.wmi", "hardware", inv.Name, "Win32_Processor"));
                    return inv;
                }
            }
        }
        catch (Exception ex)
        {
            probes.Add(Err("cpu.wmi", "hardware", ex.Message));
        }

        return new CpuInventory("unknown", 0, 0, 0, ProbeStatus.Error);
    }

    private static IReadOnlyList<GpuInventory> ReadGpus(List<ProbeResult> probes)
    {
        try
        {
            var dxgi = DxgiGpuProbe.Read();
            if (dxgi.Count > 0)
            {
                probes.Add(Ok("gpu.dxgi", "hardware", $"{dxgi.Count} adapter(s)", "DXGI"));
                return dxgi;
            }

            probes.Add(new ProbeResult("gpu.dxgi", "hardware", ProbeStatus.Unsupported, null, "No DXGI adapters", ["DXGI"]));
        }
        catch (Exception ex)
        {
            probes.Add(Err("gpu.dxgi", "hardware", ex.Message));
        }

        return [new GpuInventory("unknown", "Unknown", null, "none", ProbeStatus.Error)];
    }

    private static RamInventory ReadRam(List<ProbeResult> probes)
    {
        try
        {
            long total = 0;
            var modules = 0;
            int? speed = null;
            using var results = Wmi.Query("SELECT Capacity, Speed FROM Win32_PhysicalMemory");
            if (results is not null)
            {
                foreach (var obj in results)
                {
                    using (obj)
                    {
                        modules++;
                        total += (long)Wmi.ULong(obj, "Capacity");
                        var mhz = (int)Wmi.UInt(obj, "Speed");
                        if (mhz > 0)
                        {
                            speed = mhz;
                        }
                    }
                }
            }

            if (total == 0)
            {
                using var cs = Wmi.Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                if (cs is not null)
                {
                    foreach (var obj in cs)
                    {
                        using (obj)
                        {
                            total = (long)Wmi.ULong(obj, "TotalPhysicalMemory");
                        }
                    }
                }
            }

            var status = total > 0 ? ProbeStatus.Ok : ProbeStatus.Error;
            probes.Add(new ProbeResult(
                "ram.smbios",
                "hardware",
                status,
                null,
                $"{modules} module(s)",
                ["Win32_PhysicalMemory"]));
            return new RamInventory(total, modules, speed, status);
        }
        catch (Exception ex)
        {
            probes.Add(Err("ram.smbios", "hardware", ex.Message));
            return new RamInventory(0, 0, null, ProbeStatus.Error);
        }
    }

    private static List<VolumeInventory> ReadVolumes(List<ProbeResult> probes)
    {
        var list = new List<VolumeInventory>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                list.Add(new VolumeInventory(
                    drive.Name,
                    drive.VolumeLabel,
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    drive.DriveType));
            }

            probes.Add(Ok("storage.volumes", "storage", $"{list.Count} ready volume(s)", "DriveInfo"));
        }
        catch (Exception ex)
        {
            probes.Add(Err("storage.volumes", "storage", ex.Message));
        }

        return list;
    }

    private static BoardInventory ReadBoard(List<ProbeResult> probes)
    {
        try
        {
            var manufacturer = "";
            var product = "";
            using var board = Wmi.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            if (board is not null)
            {
                foreach (var obj in board)
                {
                    using (obj)
                    {
                        manufacturer = Wmi.String(obj, "Manufacturer");
                        product = Wmi.String(obj, "Product");
                    }
                }
            }

            var biosVersion = "";
            string? biosDate = null;
            using var bios = Wmi.Query("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
            if (bios is not null)
            {
                foreach (var obj in bios)
                {
                    using (obj)
                    {
                        biosVersion = Wmi.String(obj, "SMBIOSBIOSVersion");
                        biosDate = Wmi.String(obj, "ReleaseDate");
                    }
                }
            }

            probes.Add(Ok("board.bios", "hardware", product, "Win32_BaseBoard", "Win32_BIOS"));
            return new BoardInventory(manufacturer, product, biosVersion, biosDate, ProbeStatus.Ok);
        }
        catch (Exception ex)
        {
            probes.Add(Err("board.bios", "hardware", ex.Message));
            return new BoardInventory("unknown", "unknown", "", null, ProbeStatus.Error);
        }
    }

    private static SecurityInventory ReadSecurity(List<ProbeResult> probes)
    {
        int? vbs = null;
        var notes = new List<string>();
        try
        {
            using var dg = Wmi.Query(
                "SELECT VirtualizationBasedSecurityStatus FROM Win32_DeviceGuard",
                @"root\Microsoft\Windows\DeviceGuard");
            if (dg is not null)
            {
                foreach (var obj in dg)
                {
                    using (obj)
                    {
                        vbs = Wmi.Int(obj, "VirtualizationBasedSecurityStatus");
                    }
                }

                probes.Add(Ok("security.deviceguard", "security", $"VBS={vbs}", @"root\Microsoft\Windows\DeviceGuard"));
            }
            else
            {
                probes.Add(Denied("security.deviceguard", "security", "DeviceGuard WMI unavailable"));
            }
        }
        catch (Exception ex)
        {
            probes.Add(Denied("security.deviceguard", "security", ex.Message));
        }

        bool? defender = null;
        bool? realtime = null;
        try
        {
            using var mp = Wmi.Query(
                "SELECT AntivirusEnabled, RealTimeProtectionEnabled FROM MSFT_MpComputerStatus",
                @"root\Microsoft\Windows\Defender");
            if (mp is not null)
            {
                foreach (var obj in mp)
                {
                    using (obj)
                    {
                        defender = Wmi.Bool(obj, "AntivirusEnabled");
                        realtime = Wmi.Bool(obj, "RealTimeProtectionEnabled");
                    }
                }

                probes.Add(Ok("security.defender", "security", $"enabled={defender}", @"root\Microsoft\Windows\Defender"));
            }
            else
            {
                probes.Add(Denied("security.defender", "security", "Defender WMI unavailable (often needs admin)"));
            }
        }
        catch (Exception ex)
        {
            probes.Add(Denied("security.defender", "security", ex.Message));
        }

        bool? firewall = null;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            var raw = key?.GetValue("EnableFirewall");
            firewall = raw is int i ? i != 0 : null;
            probes.Add(Ok("security.firewall", "security", $"standardProfile={firewall}", "FirewallPolicy registry"));
        }
        catch (Exception ex)
        {
            probes.Add(Err("security.firewall", "security", ex.Message));
        }

        var status = ProbeStatus.Ok;
        if (vbs is null && defender is null)
        {
            status = ProbeStatus.Denied;
            notes.Add("Several security probes denied unelevated.");
        }

        return new SecurityInventory(vbs, defender, realtime, firewall, status, notes.Count == 0 ? null : string.Join(' ', notes));
    }

    private static readonly (string Id, string[] Names)[] ToolHints =
    [
        ("git", ["git.exe"]),
        ("node", ["node.exe"]),
        ("pwsh", ["pwsh.exe"]),
        ("docker", ["docker.exe"]),
        ("code", ["code.cmd", "code.exe"]),
        ("cursor", ["cursor.cmd", "cursor.exe"]),
        ("dotnet", ["dotnet.exe"]),
        ("winget", ["winget.exe"]),
        ("wsl", ["wsl.exe"]),
        ("vmware", ["vmware.exe"]),
        ("virtualbox", ["VirtualBox.exe"])
    ];

    private static ToolchainInventory ReadToolchain(List<ProbeResult> probes)
    {
        var present = new List<string>();
        try
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var (id, names) in ToolHints)
            {
                if (names.Any(name => paths.Any(dir => File.Exists(Path.Combine(dir.Trim('"'), name)))))
                {
                    present.Add(id);
                }
            }

            if (Directory.Exists(@"C:\Program Files (x86)\VMware\VMware Workstation")
                && !present.Contains("vmware"))
            {
                present.Add("vmware");
            }

            probes.Add(Ok("dev.toolchain", "developer", string.Join(',', present), "PATH"));
        }
        catch (Exception ex)
        {
            probes.Add(Err("dev.toolchain", "developer", ex.Message));
        }

        return new ToolchainInventory(present);
    }

    private static ProbeResult Ok(string id, string category, string message, params string[] sources) =>
        new(id, category, ProbeStatus.Ok, null, message, sources);

    private static ProbeResult Denied(string id, string category, string message) =>
        new(id, category, ProbeStatus.Denied, null, message, []);

    private static ProbeResult Err(string id, string category, string message) =>
        new(id, category, ProbeStatus.Error, null, message, []);
}

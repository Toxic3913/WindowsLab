using System.Text.Json;

namespace WindowsLab.Core;

public sealed record ProbeResult(
    string ProbeId,
    string Category,
    ProbeStatus Status,
    JsonElement? Data,
    string? Message,
    IReadOnlyList<string> Sources);

public sealed record CpuInventory(
    string Name,
    int Cores,
    int LogicalProcessors,
    uint MaxClockMhz,
    ProbeStatus Status);

public sealed record GpuInventory(
    string Name,
    string Vendor,
    long? DedicatedBytes,
    string VramSource,
    ProbeStatus Status);

public sealed record RamInventory(
    long TotalBytes,
    int ModuleCount,
    int? SpeedMhz,
    ProbeStatus Status);

public sealed record VolumeInventory(
    string Root,
    string Label,
    long TotalBytes,
    long FreeBytes,
    DriveType DriveType);

public sealed record BoardInventory(
    string Manufacturer,
    string Product,
    string BiosVersion,
    string? BiosDate,
    ProbeStatus Status);

public sealed record SecurityInventory(
    int? VirtualizationBasedSecurityStatus,
    bool? DefenderEnabled,
    bool? RealTimeProtection,
    bool? FirewallEnabled,
    ProbeStatus Status,
    string? Notes);

public sealed record ToolchainInventory(IReadOnlyList<string> Present);

public sealed record MachineInventory(
    DateTimeOffset CapturedUtc,
    OsIdentity Os,
    CpuInventory Cpu,
    IReadOnlyList<GpuInventory> Gpus,
    RamInventory Ram,
    IReadOnlyList<VolumeInventory> Volumes,
    BoardInventory Board,
    SecurityInventory Security,
    ToolchainInventory Toolchain,
    IReadOnlyList<ProbeResult> Probes);

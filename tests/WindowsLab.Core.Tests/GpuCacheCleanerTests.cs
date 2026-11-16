using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class GpuCacheCleanerTests
{
    [Fact]
    public void ResolveAdapters_skips_microsoft_basic_and_marks_nvidia_gaming()
    {
        var gpus = new[]
        {
            new GpuInventory("Microsoft Basic Render Driver", "Microsoft", 0, "dxgi", ProbeStatus.Ok),
            new GpuInventory("NVIDIA GeForce Test", "NVIDIA", 8L * 1024 * 1024 * 1024, "dxgi", ProbeStatus.Ok),
            new GpuInventory("Intel(R) UHD Graphics 770", "Intel", 128L * 1024 * 1024, "dxgi", ProbeStatus.Ok)
        };

        var adapters = GpuCacheCleaner.ResolveAdapters(gpus);
        Assert.DoesNotContain(adapters, a => a.Name.Contains("Basic", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(adapters, a => a.Vendor == "NVIDIA" && a.IsLikelyGaming);
        Assert.Contains(adapters, a => a.Vendor == "Intel" && !a.IsLikelyGaming);
    }

    [Fact]
    public void ResolveAdapters_normalizes_radeon_name_without_vendor()
    {
        var gpus = new[]
        {
            new GpuInventory("AMD Radeon RX Test", "Unknown", 4L * 1024 * 1024 * 1024, "dxgi", ProbeStatus.Ok)
        };

        var adapters = GpuCacheCleaner.ResolveAdapters(gpus);
        Assert.Contains(adapters, a => a.Vendor == "AMD" && a.IsLikelyGaming);
    }

    [Fact]
    public void ResolveAdapters_does_not_map_intel_corporation_to_amd()
    {
        var gpus = new[]
        {
            new GpuInventory("Intel(R) Arc A770 Graphics", "Intel Corporation", 16L * 1024 * 1024 * 1024, "dxgi", ProbeStatus.Ok)
        };

        var adapters = GpuCacheCleaner.ResolveAdapters(gpus);
        Assert.Contains(adapters, a => a.Vendor == "Intel" && a.IsLikelyGaming);
        Assert.DoesNotContain(adapters, a => a.Vendor == "AMD");
    }

    [Fact]
    public void Scan_returns_structure_without_throwing()
    {
        var scan = GpuCacheCleaner.Scan([]);
        Assert.NotNull(scan.Targets);
        Assert.NotNull(scan.Adapters);
        Assert.True(scan.TotalBytes >= 0);
        Assert.All(scan.Targets, t => Assert.True(t.Bytes > 0));
        Assert.All(scan.Targets, t => Assert.True(Directory.Exists(t.Path)));
    }

    [Fact]
    public void Clean_nothing_selected_is_noop()
    {
        var result = GpuCacheCleaner.Clean([]);
        Assert.Equal(0, result.TargetsCleaned);
        Assert.Equal(0, result.BytesFreed);
    }

    [Fact]
    public void FormatBytes_scales()
    {
        Assert.Equal("512 B", GpuCacheCleaner.FormatBytes(512));
        Assert.Contains("KB", GpuCacheCleaner.FormatBytes(2048), StringComparison.Ordinal);
        Assert.Contains("MB", GpuCacheCleaner.FormatBytes(5L * 1024 * 1024), StringComparison.Ordinal);
    }
}

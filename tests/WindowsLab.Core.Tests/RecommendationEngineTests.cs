using WindowsLab.Core;
using WindowsLab.Recommendations;
using WindowsLab.Tweaks;

namespace WindowsLab.Core.Tests;

public sealed class RecommendationEngineTests
{
    [Fact]
    public void Skips_experimental_and_already_matching()
    {
        var official = Make("privacy.advertising-id-off", EvidenceGrade.Official, RiskLevel.Low, ["balanced"], "0");
        var experimental = Make("myth.ram-boost", EvidenceGrade.Experimental, RiskLevel.Low, ["balanced"], "1");
        var detections = new[]
        {
            new TweakDetection(official.Id, ProbeStatus.Ok, "1", "0", false, null),
            new TweakDetection(experimental.Id, ProbeStatus.Ok, "0", "1", false, null)
        };

        var ranked = RecommendationEngine.Rank([official, experimental], detections, UserProfile.Balanced, FakeInventory());

        Assert.Contains(ranked, r => r.TweakId == official.Id);
        Assert.DoesNotContain(ranked, r => r.TweakId == experimental.Id);
    }

    [Fact]
    public void Nvidia_require_skips_when_gpu_is_intel_only()
    {
        var hags = Make(
            "gaming.hags-on",
            EvidenceGrade.Community,
            RiskLevel.Medium,
            ["gaming"],
            "2",
            requires: ["gpu.vendor=NVIDIA"]);
        var detections = new[] { new TweakDetection(hags.Id, ProbeStatus.Ok, "1", "2", false, null) };
        var inv = FakeInventory(gpus: [new GpuInventory("UHD", "Intel", 128L * 1024 * 1024, "dxgi", ProbeStatus.Ok)]);

        var ranked = RecommendationEngine.Rank([hags], detections, UserProfile.Gaming, inv);

        Assert.Empty(ranked);
    }

    private static MachineInventory FakeInventory(IReadOnlyList<GpuInventory>? gpus = null) => new(
        DateTimeOffset.UtcNow,
        OsIdentityMapper.Map(new OsIdentityFacts(10, 0, 26200, 0, "25H2", "Professional", "Windows 10 Pro", null)),
        new CpuInventory("test-cpu", 4, 8, 3000, ProbeStatus.Ok),
        gpus ?? [new GpuInventory("RTX", "NVIDIA", 8L * 1024 * 1024 * 1024, "dxgi", ProbeStatus.Ok)],
        new RamInventory(16L * 1024 * 1024 * 1024, 2, 3600, ProbeStatus.Ok),
        [new VolumeInventory(@"C:\", "Windows", 500L * 1024 * 1024 * 1024, 80L * 1024 * 1024 * 1024, DriveType.Fixed)],
        new BoardInventory("Test", "Board", "1", null, ProbeStatus.Ok),
        new SecurityInventory(2, true, true, true, ProbeStatus.Ok, null),
        new ToolchainInventory(["git", "dotnet"]),
        []);

    private static TweakDefinition Make(
        string id,
        EvidenceGrade evidence,
        RiskLevel risk,
        string[] profiles,
        string desired,
        string[]? requires = null) => new(
        id, id, "", "test", risk, evidence, [], 22000, ["*"], profiles, requires ?? [], [], false, false, false, false,
        new RegistryDetect("HKCU", "p", "n", "DWord"), desired, []);
}

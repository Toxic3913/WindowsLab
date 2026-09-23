using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.Core.Tests;

public sealed class PresetEvaluatorTests
{
    [Fact]
    public void Loads_named_packs()
    {
        var dir = FindPresets();
        var packs = PresetLoader.LoadDirectory(dir);
        Assert.True(packs.Count >= 6, $"expected >= 6 presets, got {packs.Count}");
        Assert.Contains(packs, p => p.Id == "perf.max");
        Assert.Contains(packs, p => p.Id == "stack.empresa");
        Assert.Contains(packs, p => p.Id == "stack.pruebas");
        Assert.Contains(packs, p => p.Id == "custom" && p.IsCustom);
        Assert.NotEmpty(packs.First(p => p.Id == "stack.empresa").ApplicationIds);
        Assert.Contains(packs.First(p => p.Id == "perf.max").TweakIds, id => id == "power.best-performance");
    }

    [Fact]
    public void Perf_pack_counts_ready_from_detections()
    {
        var preset = PresetLoader.LoadDirectory(FindPresets()).Single(p => p.Id == "perf.max");
        var tweak = new TweakDefinition(
            "power.best-performance",
            "power",
            "d",
            "power",
            RiskLevel.Low,
            EvidenceGrade.Official,
            [],
            22000,
            ["*"],
            ["gaming"],
            [],
            [],
            false,
            false,
            false,
            false,
            new RegistryDetect("HKLM", "p", "n", "String"),
            "ded574b5-45a0-4f42-8737-46345c09c238",
            []);
        var detection = new TweakDetection("power.best-performance", ProbeStatus.Ok, "ded574b5-45a0-4f42-8737-46345c09c238", "ded574b5-45a0-4f42-8737-46345c09c238", true, null);
        var eval = PresetEvaluator.Evaluate(preset, [tweak], [detection], []);

        Assert.True(eval.ReadyCount >= 1);
        Assert.Contains(eval.Items, i => i.Id == "power.best-performance" && i.Ready);
        Assert.Contains(eval.Items, i => i.Kind == "step");
    }

    private static string FindPresets()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "catalog", "presets");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("catalog/presets");
    }
}

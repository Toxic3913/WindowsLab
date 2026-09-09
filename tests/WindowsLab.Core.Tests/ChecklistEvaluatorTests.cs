using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.Core.Tests;

public sealed class ChecklistEvaluatorTests
{
    [Theory]
    [InlineData(@"PC-HUGO\Hugo", true)]
    [InlineData(@"WORKSTATION\localuser", true)]
    [InlineData(@"AzureAD\Someone", false)]
    [InlineData(@"MicrosoftAccount\user@live.com", false)]
    [InlineData("someone@outlook.com", false)]
    public void Local_account_heuristic(string name, bool expected)
    {
        Assert.Equal(expected, LocalAccountDetector.IsLikelyLocal(name));
    }

    [Fact]
    public void Telemetry_required_is_ok_on_pro()
    {
        var registry = new FakeRegistry(("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 1));
        var result = Eval("telemetry.allow-required", "telemetry", ChecklistPolicy.Recommend, registry);

        Assert.Equal(ChecklistVerdict.Ok, result.Verdict);
        Assert.Equal("1", result.Actual);
    }

    [Fact]
    public void Telemetry_zero_is_info_not_ok_on_consumer()
    {
        var registry = new FakeRegistry(("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0));
        var result = Eval("telemetry.allow-required", "telemetry", ChecklistPolicy.Recommend, registry);

        Assert.Equal(ChecklistVerdict.Info, result.Verdict);
        Assert.Contains("no es efectivo", result.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Telemetry_full_is_gap()
    {
        var registry = new FakeRegistry(("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 3));
        var result = Eval("telemetry.allow-required", "telemetry", ChecklistPolicy.Recommend, registry);

        Assert.Equal(ChecklistVerdict.Gap, result.Verdict);
    }

    [Fact]
    public void WinDefend_disabled_is_gap()
    {
        var registry = new FakeRegistry(("HKLM", @"SYSTEM\CurrentControlSet\Services\WinDefend", "Start", 4));
        var item = new ChecklistDefinition(
            "security.windefend-service",
            "Seguridad",
            "WinDefend",
            "d",
            "h",
            ChecklistPolicy.NeverDisable,
            "service",
            41,
            null,
            null,
            null,
            null,
            "WinDefend",
            null,
            null);

        var result = ChecklistEvaluator.Evaluate([item], SampleInventory(), registry, @"PC\user").Single();

        Assert.Equal(ChecklistVerdict.Gap, result.Verdict);
    }

    [Fact]
    public void DiagTrack_auto_is_info_not_gap()
    {
        var registry = new FakeRegistry(("HKLM", @"SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 2));
        var item = new ChecklistDefinition(
            "telemetry.no-kill-diagtrack",
            "Telemetría",
            "DiagTrack",
            "d",
            "h",
            ChecklistPolicy.NeverDisable,
            "service",
            29,
            null,
            null,
            null,
            null,
            "DiagTrack",
            null,
            null);

        var result = ChecklistEvaluator.Evaluate([item], SampleInventory(), registry, @"PC\user").Single();

        Assert.Equal(ChecklistVerdict.Info, result.Verdict);
    }

    [Fact]
    public void Loads_baseline_catalog()
    {
        var dir = FindChecklists();
        var items = ChecklistLoader.LoadDirectory(dir);
        Assert.True(items.Count >= 40, $"expected >= 40 checklist items, got {items.Count}");
        Assert.Contains(items, i => i.Id == "account.local");
        Assert.Contains(items, i => i.DetectKind == "telemetry");
        Assert.Contains(items, i => i.Policy == ChecklistPolicy.NeverDisable);
    }

    private static ChecklistResult Eval(string id, string kind, ChecklistPolicy policy, IRegistryReader registry)
    {
        var item = new ChecklistDefinition(id, "s", "t", "d", "h", policy, kind, 1, null, null, null, null, null, null, null);
        return ChecklistEvaluator.Evaluate([item], SampleInventory(), registry, @"PC-HUGO\Hugo").Single();
    }

    private static MachineInventory SampleInventory()
    {
        var os = OsIdentityMapper.Map(new OsIdentityFacts(10, 0, 26200, 9168, "25H2", "Professional", "Windows 10 Pro", null));
        return new MachineInventory(
            DateTimeOffset.UtcNow,
            os,
            new CpuInventory("cpu", 4, 8, 3000, ProbeStatus.Ok),
            [],
            new RamInventory(16L * 1024 * 1024 * 1024, 2, 3600, ProbeStatus.Ok),
            [new VolumeInventory(@"C:\", "Windows", 200L * 1024 * 1024 * 1024, 50L * 1024 * 1024 * 1024, DriveType.Fixed)],
            new BoardInventory("m", "p", "1", null, ProbeStatus.Ok),
            new SecurityInventory(2, true, true, true, ProbeStatus.Ok, null),
            new ToolchainInventory(["git"]),
            []);
    }

    private static string FindChecklists()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "catalog", "checklists");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("catalog/checklists");
    }

    private sealed class FakeRegistry : IRegistryReader
    {
        private readonly Dictionary<string, object?> _map = new(StringComparer.OrdinalIgnoreCase);

        public FakeRegistry(params (string Hive, string Path, string Name, object? Value)[] values)
        {
            foreach (var (hive, path, name, value) in values)
            {
                _map[$"{hive}|{path}|{name}"] = value;
            }
        }

        public object? GetValue(string hive, string path, string name) =>
            _map.TryGetValue($"{hive}|{path}|{name}", out var v) ? v : null;
    }
}

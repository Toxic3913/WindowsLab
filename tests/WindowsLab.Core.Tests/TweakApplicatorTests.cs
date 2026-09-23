using Microsoft.Win32;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.Core.Tests;

public sealed class TweakApplicatorTests
{
    [Fact]
    public void IsLabEligible_rejects_hklm_and_high_risk()
    {
        var ok = Sample("privacy.advertising-id-off", RiskLevel.Low, EvidenceGrade.Official, "HKCU");
        Assert.True(TweakApplicator.IsLabEligible(ok));

        var hklm = Sample("developer.long-paths", RiskLevel.Low, EvidenceGrade.Official, "HKLM");
        Assert.False(TweakApplicator.IsLabEligible(hklm));

        var high = Sample("x.bad", RiskLevel.High, EvidenceGrade.Official, "HKCU");
        Assert.False(TweakApplicator.IsLabEligible(high));

        var experimental = Sample("x.exp", RiskLevel.Low, EvidenceGrade.Experimental, "HKCU");
        Assert.False(TweakApplicator.IsLabEligible(experimental));
    }

    [Fact]
    public void IsApplyEligible_allows_hklm_official_low()
    {
        var hklm = Sample("developer.long-paths", RiskLevel.Low, EvidenceGrade.Official, "HKLM");
        Assert.False(TweakApplicator.IsLabEligible(hklm));
        Assert.True(TweakApplicator.IsApplyEligible(hklm));
        Assert.True(TweakApplicator.NeedsElevation(hklm));
    }

    [Fact]
    public void IsApplyEligible_allows_high_security_registry_hklm()
    {
        var defender = new TweakDefinition(
            "security.defender-realtime-off",
            "Defender realtime off",
            "test",
            "security",
            RiskLevel.High,
            EvidenceGrade.Official,
            [],
            22000,
            [],
            [],
            [],
            [],
            RequiresReboot: true,
            AffectsSecurity: true,
            AffectsUpdates: false,
            AffectsCompatibility: false,
            new RegistryDetect(
                "HKLM",
                @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection",
                "DisableRealtimeMonitoring",
                "DWord"),
            "1",
            [new TweakOp(
                "registry",
                "HKLM",
                @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection",
                "DisableRealtimeMonitoring",
                "DWord",
                "1")]);

        Assert.False(TweakApplicator.IsLabEligible(defender));
        Assert.True(TweakApplicator.IsApplyEligible(defender));
        Assert.True(TweakApplicator.NeedsElevation(defender));
    }

    [Fact]
    public void ProtectedServices_hard_blocks_sysmain_not_generic_registry()
    {
        Assert.True(ProtectedServices.IsHardBlocked("SysMain"));
        Assert.True(ProtectedServices.IsHardBlocked("WSearch"));
        Assert.True(ProtectedServices.IsHardBlocked("DiagTrack"));
        Assert.True(ProtectedServices.IsDefenderFamily("WinDefend"));
        Assert.True(ProtectedServices.IsBlocked("WinDefend"));
        Assert.False(ProtectedServices.IsHardBlocked("WinDefend"));
    }

    [Fact]
    public void Apply_writes_hkcu_and_persists_backup_then_rollback()
    {
        var store = new MemoryRegistry();
        store.Set("HKCU", @"Software\WindowsLabTest", "Enabled", 1);
        var tweak = Sample("privacy.advertising-id-off", RiskLevel.Low, EvidenceGrade.Official, "HKCU",
            path: @"Software\WindowsLabTest", name: "Enabled", desired: "0", valueKind: "DWord");

        var result = TweakApplicator.Apply(tweak, store, store);
        Assert.Equal(ApplyOutcome.Ok, result.Outcome);
        Assert.NotNull(result.Backup);
        Assert.Equal("1", result.Backup!.PreviousValue);
        Assert.Equal(0, store.GetValue("HKCU", @"Software\WindowsLabTest", "Enabled"));

        var dir = Path.Combine(Path.GetTempPath(), "WindowsLab-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = TweakApplicator.PersistBackup(result.Backup, dir);
            Assert.True(File.Exists(path));

            var rb = TweakApplicator.Rollback(result.Backup, store);
            Assert.True(rb.Succeeded);
            Assert.Equal(1, store.GetValue("HKCU", @"Software\WindowsLabTest", "Enabled"));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Apply_dry_run_does_not_write()
    {
        var store = new MemoryRegistry();
        store.Set("HKCU", @"Software\WindowsLabTest", "Enabled", 1);
        var tweak = Sample("privacy.advertising-id-off", RiskLevel.Low, EvidenceGrade.Official, "HKCU",
            path: @"Software\WindowsLabTest", name: "Enabled", desired: "0");

        var result = TweakApplicator.Apply(tweak, store, store, dryRun: true);
        Assert.Equal(ApplyOutcome.Ok, result.Outcome);
        Assert.Equal(1, store.GetValue("HKCU", @"Software\WindowsLabTest", "Enabled"));
    }

    [Fact]
    public void Apply_already_matched_skips_write()
    {
        var store = new MemoryRegistry();
        store.Set("HKCU", @"Software\WindowsLabTest", "Enabled", 0);
        var tweak = Sample("privacy.advertising-id-off", RiskLevel.Low, EvidenceGrade.Official, "HKCU",
            path: @"Software\WindowsLabTest", name: "Enabled", desired: "0");

        var result = TweakApplicator.Apply(tweak, store, store);
        Assert.Equal(ApplyOutcome.AlreadyMatched, result.Outcome);
    }

    private static TweakDefinition Sample(
        string id,
        RiskLevel risk,
        EvidenceGrade evidence,
        string hive,
        string path = @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
        string name = "Enabled",
        string desired = "0",
        string valueKind = "DWord") =>
        new(
            id,
            "Test",
            "desc",
            "privacy",
            risk,
            evidence,
            [],
            22000,
            ["*"],
            ["balanced"],
            [],
            [],
            RequiresReboot: false,
            AffectsSecurity: false,
            AffectsUpdates: false,
            AffectsCompatibility: false,
            new RegistryDetect(hive, path, name, valueKind),
            desired,
            [new TweakOp("registry", hive, path, name, valueKind, desired)]);

    private sealed class MemoryRegistry : IRegistryReader, IRegistryWriter
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        private static string Key(string hive, string path, string name) => $"{hive}|{path}|{name}";

        public void Set(string hive, string path, string name, object? value) =>
            _values[Key(hive, path, name)] = value;

        public object? GetValue(string hive, string path, string name) =>
            _values.TryGetValue(Key(hive, path, name), out var v) ? v : null;

        public void SetValue(string hive, string path, string name, object value, RegistryValueKind kind) =>
            _values[Key(hive, path, name)] = value;

        public void DeleteValue(string hive, string path, string name) =>
            _values.Remove(Key(hive, path, name));
    }
}

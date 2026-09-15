using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.Core.Tests;

public sealed class TweakDetectorTests
{
    [Fact]
    public void Detect_matches_desired_dword()
    {
        var tweak = Sample("explorer.show-file-extensions", "0");
        var registry = new FakeRegistry(("HKCU", tweak.Detect.Path, "HideFileExt", 0));

        var result = TweakDetector.Detect(tweak, registry);

        Assert.Equal(ProbeStatus.Ok, result.Status);
        Assert.True(result.MatchesDesired);
        Assert.Equal("0", result.ActualDisplay);
    }

    [Fact]
    public void Detect_missing_value_does_not_match()
    {
        var tweak = Sample("explorer.show-file-extensions", "0");
        var result = TweakDetector.Detect(tweak, new FakeRegistry());

        Assert.False(result.MatchesDesired);
        Assert.Equal("(missing)", result.ActualDisplay);
    }

    private static TweakDefinition Sample(string id, string desired) => new(
        id,
        "t",
        "d",
        "explorer",
        RiskLevel.Low,
        EvidenceGrade.Official,
        [],
        22000,
        ["*"],
        ["balanced"],
        [],
        [],
        false,
        false,
        false,
        false,
        new RegistryDetect("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", "DWord"),
        desired,
        []);

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

using WindowsLab.Core;

namespace WindowsLab.Core.Tests;

public sealed class OperatorSettingsStoreTests
{
    [Fact]
    public void Round_trips_profile_and_last_preset()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowsLab-tests", Guid.NewGuid().ToString("N"), "operator.json");
        try
        {
            var saved = new OperatorSettings("gaming", "perf.max", DateTimeOffset.UnixEpoch, "en");
            OperatorSettingsStore.Save(saved, path);
            var loaded = OperatorSettingsStore.Load(path);
            Assert.Equal("gaming", loaded.Profile);
            Assert.Equal("perf.max", loaded.LastPresetId);
            Assert.Equal("en", loaded.Language);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Missing_file_returns_balanced()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowsLab-tests", Guid.NewGuid().ToString("N"), "missing.json");
        var loaded = OperatorSettingsStore.Load(path);
        Assert.Equal("balanced", loaded.Profile);
        Assert.Null(loaded.LastPresetId);
    }

    [Fact]
    public void Describes_four_channels()
    {
        var channels = ConfigChannels.Describe(@"D:\WindowsLab\catalog\tweaks");
        Assert.Equal(4, channels.Count);
        Assert.Contains(channels, c => c.Id == "user");
        Assert.Contains(channels, c => c.Id == "machine");
        Assert.Contains(channels, c => c.Id == "catalog");
        Assert.Contains(channels, c => c.Id == "lab");
        Assert.Contains(channels, c => c.Path.Contains("catalog", StringComparison.OrdinalIgnoreCase));
    }
}

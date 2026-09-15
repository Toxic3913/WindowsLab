using WindowsLab.Applications;
using WindowsLab.Core;
using WindowsLab.Recommendations;

namespace WindowsLab.Core.Tests;

public sealed class AppRecommendationEngineTests
{
    [Fact]
    public void Balanced_ranks_brave_above_chrome()
    {
        var catalog = ApplicationCatalogLoader.LoadDirectory(AppsDir());
        var ranked = AppRecommendationEngine.Rank(catalog, UserProfile.Balanced, categoryFilter: "browser");
        Assert.NotEmpty(ranked);
        var brave = ranked.Single(r => r.AppId == "app.browser.brave");
        var chrome = ranked.Single(r => r.AppId == "app.browser.chrome");
        Assert.True(brave.Score > chrome.Score, $"brave={brave.Score} chrome={chrome.Score}");
    }

    [Fact]
    public void Gaming_can_rank_chrome_above_brave_on_perf_ecosystem()
    {
        var catalog = ApplicationCatalogLoader.LoadDirectory(AppsDir());
        var ranked = AppRecommendationEngine.Rank(catalog, UserProfile.Gaming, categoryFilter: "browser");
        var brave = ranked.Single(r => r.AppId == "app.browser.brave");
        var chrome = ranked.Single(r => r.AppId == "app.browser.chrome");
        Assert.True(chrome.Score > brave.Score, $"chrome={chrome.Score} brave={brave.Score}");
    }

    [Fact]
    public void Skips_unknown_and_experimental()
    {
        var bad = new ApplicationDefinition(
            "app.browser.myth",
            "Myth",
            "",
            "Myth.Browser",
            "browser",
            EvidenceGrade.Experimental,
            [],
            RiskLevel.Low,
            new AppAxes(1, 1, 1, 1, 1),
            ["balanced"],
            AppInstallScope.User,
            ["Myth"]);
        var ranked = AppRecommendationEngine.Rank([bad], UserProfile.Balanced);
        Assert.Empty(ranked);
    }

    [Fact]
    public void Why_never_claims_absolute_best()
    {
        var catalog = ApplicationCatalogLoader.LoadDirectory(AppsDir());
        var ranked = AppRecommendationEngine.Rank(catalog, UserProfile.Balanced, categoryFilter: "browser");
        Assert.All(ranked, r => Assert.Contains("Ranking relativo", r.Why, StringComparison.OrdinalIgnoreCase));
        Assert.All(ranked, r => Assert.DoesNotContain("mejor navegador absoluto", r.Why, StringComparison.OrdinalIgnoreCase));
        Assert.All(ranked, r => Assert.DoesNotContain("best browser absolute", r.Why, StringComparison.OrdinalIgnoreCase));
    }

    private static string AppsDir()
    {
        var dir = CatalogLocator.FindApplicationsDirectory();
        Assert.NotNull(dir);
        return dir!;
    }
}

public sealed class ThemeResolverTests
{
    [Theory]
    [InlineData(UiThemePreference.Dark, UiThemePreference.Dark)]
    [InlineData(UiThemePreference.Light, UiThemePreference.Light)]
    public void ResolveEffective_passthrough(UiThemePreference input, UiThemePreference expected)
    {
        Assert.Equal(expected, ThemeResolver.ResolveEffective(input));
    }

    [Fact]
    public void System_resolves_to_dark_or_light()
    {
        var effective = ThemeResolver.ResolveEffective(UiThemePreference.System);
        Assert.True(effective is UiThemePreference.Dark or UiThemePreference.Light);
    }

    [Fact]
    public void OperatorSettings_round_trips_theme()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowsLab-tests", Guid.NewGuid().ToString("N"), "operator.json");
        try
        {
            OperatorSettingsStore.Save(new OperatorSettings("balanced", null, DateTimeOffset.UnixEpoch, "es", "light"), path);
            var loaded = OperatorSettingsStore.Load(path);
            Assert.Equal("light", loaded.Theme);
            Assert.Equal(UiThemePreference.Light, OperatorSettingsStore.ParseTheme(loaded.Theme));
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
}

public sealed class ApplicationCatalogLoaderTests
{
    [Fact]
    public void Loads_browsers_and_tools()
    {
        var dir = CatalogLocator.FindApplicationsDirectory();
        Assert.NotNull(dir);
        var apps = ApplicationCatalogLoader.LoadDirectory(dir!);
        Assert.Contains(apps, a => a.Id == "app.browser.brave");
        Assert.Contains(apps, a => a.Id == "app.tool.git");
        Assert.All(apps, a => Assert.False(string.IsNullOrWhiteSpace(a.WingetId)));
    }
}

public sealed class InstalledAppDetectorTests
{
    [Theory]
    [InlineData("Git", "Git", true)]
    [InlineData("Git for Windows", "Git", true)]
    [InlineData("GitHub Desktop", "Git", false)]
    [InlineData("Google Chrome", "Chrome", true)]
    [InlineData("Chromium", "Chrome", false)]
    [InlineData("PowerShell 7.4.0", "PowerShell 7", true)]
    [InlineData("Windows PowerShell", "PowerShell 7", false)]
    public void NameMatches_word_boundaries(string display, string detect, bool expected)
    {
        Assert.Equal(expected, InstalledAppDetector.NameMatches(display, detect));
    }
}

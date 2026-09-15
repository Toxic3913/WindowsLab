using System.Windows;
using WindowsLab.Core;

namespace WindowsLab.App;

public static class ThemeService
{
    private const string DarkDict = "Themes/Dark.xaml";
    private const string LightDict = "Themes/Light.xaml";

    public static UiThemePreference CurrentPreference { get; private set; } = UiThemePreference.Dark;

    public static bool IsLightEffective => ThemeResolver.ResolveEffective(CurrentPreference) == UiThemePreference.Light;

    public static void Apply(UiThemePreference preference, Application? app = null)
    {
        CurrentPreference = preference;
        app ??= Application.Current;
        if (app is null)
        {
            return;
        }

        var effective = ThemeResolver.ResolveEffective(preference);
        var uri = new Uri(effective == UiThemePreference.Light ? LightDict : DarkDict, UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null
                && (d.Source.OriginalString.Contains("Themes/Dark", StringComparison.OrdinalIgnoreCase)
                    || d.Source.OriginalString.Contains("Themes/Light", StringComparison.OrdinalIgnoreCase)));
        if (existing is not null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }

        app.Resources.MergedDictionaries.Insert(0, dict);
    }
}

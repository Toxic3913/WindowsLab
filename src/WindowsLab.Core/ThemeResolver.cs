using Microsoft.Win32;

namespace WindowsLab.Core;

public static class ThemeResolver
{
    public static UiThemePreference ResolveEffective(UiThemePreference preference) =>
        preference == UiThemePreference.System ? DetectSystemTheme() : preference;

    public static UiThemePreference DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
            {
                return i == 0 ? UiThemePreference.Dark : UiThemePreference.Light;
            }
        }
        catch
        {
            // fall through
        }

        return UiThemePreference.Dark;
    }

    public static string ToSettingsString(UiThemePreference preference) => preference switch
    {
        UiThemePreference.Light => "light",
        UiThemePreference.System => "system",
        _ => "dark"
    };
}

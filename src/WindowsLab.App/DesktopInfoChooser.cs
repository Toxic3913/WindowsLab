using WindowsLab.Core;

namespace WindowsLab.App;

public enum DesktopOverlayChoice
{
    DesktopInfo,
    BgInfo
}

public sealed record DesktopOverlayRecommendation(
    DesktopOverlayChoice Recommended,
    string Title,
    string Reason,
    IReadOnlyList<string> Conflicts,
    bool HasWallpaperConflict);

/// <summary>
/// Picks DesktopInfo (overlay, wallpaper-safe) vs BGInfo (paints wallpaper).
/// </summary>
public static class DesktopInfoChooser
{
    public static DesktopOverlayRecommendation Recommend()
    {
        var conflicts = QuickTools.DetectWallpaperConflicts();
        var hasConflict = conflicts.Count > 0;
        if (hasConflict)
        {
            return new DesktopOverlayRecommendation(
                DesktopOverlayChoice.DesktopInfo,
                Loc.IsEnglish ? "Recommended: DesktopInfo" : "Recomendado: DesktopInfo",
                Loc.IsEnglish
                    ? $"Wallpaper apps detected ({string.Join(", ", conflicts)}). DesktopInfo is an overlay — it does not replace your wallpaper, so Wallpaper Engine / Lively keep working. Use BGInfo only if you accept pausing those apps."
                    : $"Apps de fondo detectadas ({string.Join(", ", conflicts)}). DesktopInfo es un overlay: no sustituye el fondo, así que Wallpaper Engine / Lively siguen. Usa BGInfo solo si aceptas pausar esas apps.",
                conflicts,
                true);
        }

        return new DesktopOverlayRecommendation(
            DesktopOverlayChoice.BgInfo,
            Loc.IsEnglish ? "Recommended: BGInfo" : "Recomendado: BGInfo",
            Loc.IsEnglish
                ? "No wallpaper controllers detected. BGInfo can paint system info on the desktop wallpaper (Sysinternals). DesktopInfo remains available as a live floating overlay."
                : "No hay controladores de fondo activos. BGInfo puede pintar info en el wallpaper (Sysinternals). DesktopInfo sigue disponible como overlay flotante en vivo.",
            conflicts,
            false);
    }
}

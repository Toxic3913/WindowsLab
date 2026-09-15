namespace WindowsLab.Core;

public static class Loc
{
    private static string _lang = "es";

    public static string Language
    {
        get => _lang;
        set => _lang = string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
    }

    public static bool IsEnglish => _lang == "en";

    public static string T(string key) =>
        (IsEnglish ? En : Es).TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> Es = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nav.home"] = "Inicio",
        ["nav.config"] = "Configurar",
        ["nav.resources"] = "Recursos",
        ["nav.system"] = "Sistema",
        ["nav.security"] = "Seguridad",
        ["nav.tweaks"] = "Tweaks",
        ["nav.advice"] = "Consejos",
        ["nav.checklist"] = "Checklist",
        ["nav.apps"] = "Aplicaciones",
        ["nav.settings"] = "Ajustes",
        ["subtitle"] = "Beta 0.2 — lab apply + apps",
        ["profile"] = "Perfil",
        ["theme.label"] = "Tema",
        ["theme.dark"] = "Oscuro",
        ["theme.light"] = "Claro",
        ["theme.system"] = "Sistema",
        ["apps.hint"] = "Ranking multi-eje (privacidad, telemetría, seguridad, rendimiento, ecosistema) según perfil. Instalar usa winget con confirmación. No fija el navegador predeterminado.",
        ["apps.install"] = "Instalar selección",
        ["apps.defaults"] = "Apps predeterminadas (Windows)",
        ["apps.filter"] = "Categoría",
        ["home.packs"] = "Packs — pulsa un botón para configurar",
        ["home.probes"] = "Sondas",
        ["home.loading"] = "Cargando inventario y catálogo… (no hace falta administrador)",
        ["home.loadingHint"] = "La primera carga puede tardar en VMware. Si falla: %LocalAppData%\\WindowsLab\\crash.log",
        ["config.hint"] = "Elige un pack. Activar abre Windows. Aplicar escribe solo tweaks HKCU LOW (con backup).",
        ["btn.activatePack"] = "Abrir en Windows",
        ["btn.simulatePack"] = "Simular pack",
        ["btn.activateRow"] = "Abrir fila",
        ["btn.applyPack"] = "Aplicar pack (HKCU)",
        ["btn.applyDisabled"] = "Solo ítems elegibles: HKCU, LOW, OFFICIAL/STRONG.",
        ["btn.applyTweak"] = "Aplicar selección",
        ["resources.title"] = "Recursos en vivo",
        ["resources.hint"] = "Estándar: BGInfo (Sysinternals). Overlay WindowsLab: solo lectura, no pinta el wallpaper.",
        ["btn.desktopInfo"] = "DesktopInfo overlay",
        ["btn.bginfo"] = "BGInfo oficial (Sysinternals)",
        ["btn.bginfo.activate"] = "Activar BGInfo",
        ["btn.bginfo.docs"] = "Docs BGInfo",
        ["btn.activateWindows"] = "Activar Windows",
        ["btn.libreoffice"] = "Descargar LibreOffice",
        ["home.quick"] = "Acciones rápidas",
        ["advice.hint"] = "Consejos del perfil + guía de procesos en segundo plano. No matamos Defender/Search/SysMain.",
        ["advice.bg.title"] = "Procesos en segundo plano",
        ["settings.channels"] = "Canales de configuración",
        ["settings.channelsHint"] = "Preferencias UI + backups de apply en %LocalAppData%\\WindowsLab\\backups.",
        ["btn.openUser"] = "Abrir carpeta de usuario",
        ["btn.openMachine"] = "Abrir carpeta de máquina",
        ["lang.label"] = "Idioma",
        ["telemetry.limit"] = "En Home/Pro no existe telemetría cero: el mínimo oficial es Required (1).",
        ["tools.godmode"] = "Modo Dios (todas las applets)",
        ["tools.godmodeHow"] = "Abre el panel clásico con todas las opciones. No es un bypass.",
        ["tweaks.hint"] = "Vista experta (tabla). Para flujo simple usa Checklist → Activar. Apply = HKCU con backup.",
        ["checklist.hint"] = "Checklist simple: Activar abre Configuración o aplica HKCU si está enlazado.",
    };

    private static readonly Dictionary<string, string> En = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nav.home"] = "Home",
        ["nav.config"] = "Configure",
        ["nav.resources"] = "Resources",
        ["nav.system"] = "System",
        ["nav.security"] = "Security",
        ["nav.tweaks"] = "Tweaks",
        ["nav.advice"] = "Advice",
        ["nav.checklist"] = "Checklist",
        ["nav.apps"] = "Applications",
        ["nav.settings"] = "Settings",
        ["subtitle"] = "Beta 0.2 — lab apply + apps",
        ["profile"] = "Profile",
        ["theme.label"] = "Theme",
        ["theme.dark"] = "Dark",
        ["theme.light"] = "Light",
        ["theme.system"] = "System",
        ["apps.hint"] = "Multi-axis ranking (privacy, telemetry, security, performance, ecosystem) by profile. Install uses winget with confirmation. Does not set the default browser.",
        ["apps.install"] = "Install selection",
        ["apps.defaults"] = "Default apps (Windows)",
        ["apps.filter"] = "Category",
        ["home.packs"] = "Packs — click a button to configure",
        ["home.probes"] = "Probes",
        ["home.loading"] = "Loading inventory and catalog… (admin not required)",
        ["home.loadingHint"] = "First load may take a few seconds in VMware. On failure: %LocalAppData%\\WindowsLab\\crash.log",
        ["config.hint"] = "Pick a pack. Open in Windows, or Apply writes eligible HKCU LOW tweaks (with backup).",
        ["btn.activatePack"] = "Open in Windows",
        ["btn.simulatePack"] = "Simulate pack",
        ["btn.activateRow"] = "Open row",
        ["btn.applyPack"] = "Apply pack (HKCU)",
        ["btn.applyDisabled"] = "Only eligible items: HKCU, LOW, OFFICIAL/STRONG.",
        ["btn.applyTweak"] = "Apply selection",
        ["resources.title"] = "Live resources",
        ["resources.hint"] = "Industry standard: BGInfo (Sysinternals). WindowsLab overlay is read-only and does not paint wallpaper.",
        ["btn.desktopInfo"] = "DesktopInfo overlay",
        ["btn.bginfo"] = "Official BGInfo (Sysinternals)",
        ["btn.bginfo.activate"] = "Enable BGInfo",
        ["btn.bginfo.docs"] = "BGInfo docs",
        ["btn.activateWindows"] = "Activate Windows",
        ["btn.libreoffice"] = "Download LibreOffice",
        ["home.quick"] = "Quick actions",
        ["advice.hint"] = "Profile advice + background-process tips. We do not kill Defender/Search/SysMain.",
        ["advice.bg.title"] = "Background processes",
        ["settings.channels"] = "Configuration channels",
        ["settings.channelsHint"] = "UI prefs + apply backups under %LocalAppData%\\WindowsLab\\backups.",
        ["btn.openUser"] = "Open user folder",
        ["btn.openMachine"] = "Open machine folder",
        ["lang.label"] = "Language",
        ["telemetry.limit"] = "Home/Pro cannot fully disable telemetry: official floor is Required (1).",
        ["tools.godmode"] = "God Mode (all applets)",
        ["tools.godmodeHow"] = "Opens the classic panel with every Control Panel applet. Not a bypass.",
        ["tweaks.hint"] = "Expert table view. For a simple flow use Checklist → Activate. Apply = HKCU with backup.",
        ["checklist.hint"] = "Simple checklist: Activate opens Settings or applies linked HKCU tweaks.",
    };
}

using System.Text.Json;

namespace WindowsLab.Core;

public sealed record ConfigChannel(
    string Id,
    string Title,
    string Path,
    string Role,
    bool WritableInBeta0);

public sealed record OperatorSettings(
    string Profile,
    string? LastPresetId,
    DateTimeOffset SavedUtc,
    string Language = "es",
    string Theme = "dark");

public static class ConfigChannels
{
    public const string FolderName = "WindowsLab";

    public static string UserRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

    public static string MachineRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), FolderName);

    public static string OperatorSettingsPath => Path.Combine(UserRoot, "operator.json");

    public static string LabVmRoot => @"D:\VM";

    public static IReadOnlyList<ConfigChannel> Describe(string? catalogTweaksDirectory)
    {
        var catalog = catalogTweaksDirectory is null
            ? "(catálogo no encontrado)"
            : Path.GetDirectoryName(catalogTweaksDirectory) ?? catalogTweaksDirectory;
        return
        [
            new ConfigChannel("user", "Usuario (preferencias)", UserRoot, "Perfil, último pack, operator.json. No es un backup.", true),
            new ConfigChannel("machine", "Máquina (runtime)", MachineRoot, "Backups, logs y reports (fases 3–4). Beta 0 solo crea la carpeta.", true),
            new ConfigChannel("catalog", "Catálogo (solo lectura)", catalog, "Tweaks, checklists y packs JSON. No se edita desde la UI.", false),
            new ConfigChannel("lab", "Laboratorio VM", LabVmRoot, "OVA, VMDK y snapshots. No mezclar con ProgramData.", false)
        ];
    }

    public static void EnsureRuntimeFolders()
    {
        Directory.CreateDirectory(UserRoot);
        Directory.CreateDirectory(MachineRoot);
        Directory.CreateDirectory(Path.Combine(UserRoot, "backups"));
        Directory.CreateDirectory(Path.Combine(MachineRoot, "reports"));
        Directory.CreateDirectory(Path.Combine(MachineRoot, "tmp"));
    }
}

public static class OperatorSettingsStore
{
    public static OperatorSettings Load(string? path = null)
    {
        var file = path ?? ConfigChannels.OperatorSettingsPath;
        if (!File.Exists(file))
        {
            return new OperatorSettings("balanced", null, DateTimeOffset.UnixEpoch, "es", "dark");
        }

        try
        {
            var json = File.ReadAllText(file);
            var loaded = JsonSerializer.Deserialize<OperatorSettings>(json, JsonDefaults.Options)
                   ?? new OperatorSettings("balanced", null, DateTimeOffset.UnixEpoch, "es", "dark");
            return NormalizeTheme(loaded);
        }
        catch (JsonException)
        {
            return new OperatorSettings("balanced", null, DateTimeOffset.UnixEpoch, "es", "dark");
        }
    }

    private static OperatorSettings NormalizeTheme(OperatorSettings settings)
    {
        var theme = settings.Theme?.Trim().ToLowerInvariant() switch
        {
            "light" => "light",
            "system" => "system",
            _ => "dark"
        };
        return settings with { Theme = theme, Language = string.Equals(settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es" };
    }

    public static UiThemePreference ParseTheme(string? theme) => theme?.Trim().ToLowerInvariant() switch
    {
        "light" => UiThemePreference.Light,
        "system" => UiThemePreference.System,
        _ => UiThemePreference.Dark
    };

    public static void Save(OperatorSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = NormalizeTheme(settings);
        var file = path ?? ConfigChannels.OperatorSettingsPath;
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(file, JsonSerializer.Serialize(settings, JsonDefaults.Options));
    }
}

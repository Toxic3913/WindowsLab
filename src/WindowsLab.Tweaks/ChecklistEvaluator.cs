using System.Diagnostics;
using System.Security.Principal;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public static class LocalAccountDetector
{
    public static bool IsLikelyLocal(string windowsIdentityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsIdentityName);
        if (windowsIdentityName.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        if (windowsIdentityName.StartsWith("AzureAD\\", StringComparison.OrdinalIgnoreCase)
            || windowsIdentityName.StartsWith("MicrosoftAccount\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return windowsIdentityName.Contains('\\', StringComparison.Ordinal);
    }
}

public static class ChecklistEvaluator
{
    public static IReadOnlyList<ChecklistResult> Evaluate(
        IReadOnlyList<ChecklistDefinition> items,
        MachineInventory inventory,
        IRegistryReader registry,
        string? windowsIdentityName = null)
    {
        var name = windowsIdentityName ?? WindowsIdentity.GetCurrent().Name;
        return items.Select(item => EvaluateOne(item, inventory, registry, name)).ToArray();
    }

    private static ChecklistResult EvaluateOne(
        ChecklistDefinition item,
        MachineInventory inventory,
        IRegistryReader registry,
        string windowsIdentityName)
    {
        var kind = item.DetectKind.ToLowerInvariant();
        return kind switch
        {
            "localaccount" => Account(item, windowsIdentityName),
            "registry" => Registry(item, registry),
            "telemetry" => Telemetry(item, registry),
            "process" => ProcessItem(item),
            "service" => ServiceItem(item, registry),
            "toolchain" => Tool(item, inventory),
            "defender" => Defender(item, inventory),
            "firewall" => Firewall(item, inventory),
            "diskfree" => Disk(item, inventory),
            _ => new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Info, "manual", item.DesiredEquals ?? "", item.HowTo, item.Policy, item.Description, item.SettingsUri)
        };
    }

    private static ChecklistResult Account(ChecklistDefinition item, string name)
    {
        var local = LocalAccountDetector.IsLikelyLocal(name);
        var desired = "cuenta local (PC\\usuario)";
        if (item.Policy == ChecklistPolicy.Recommend)
        {
            return new ChecklistResult(
                item.Id, item.Section, item.Title,
                local ? ChecklistVerdict.Ok : ChecklistVerdict.Gap,
                name,
                desired,
                item.HowTo,
                item.Policy,
                local ? "Sesión local." : "Parece cuenta Microsoft/Entra. Pasa a cuenta local en Configuración > Cuentas.",
                item.SettingsUri);
        }

        return new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Info, name, desired, item.HowTo, item.Policy, item.Description, item.SettingsUri);
    }

    private static ChecklistResult Telemetry(ChecklistDefinition item, IRegistryReader registry)
    {
        object? raw = null;
        try
        {
            raw = registry.GetValue("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry")
                  ?? registry.GetValue("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry");
        }
        catch (UnauthorizedAccessException)
        {
            return new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Unknown, "denied", "1", item.HowTo, item.Policy, "HKLM denegado. El mínimo oficial en Pro/Home es Required (1); 0 no se honra.", item.SettingsUri);
        }

        var actual = Format(raw);
        var (verdict, note) = actual switch
        {
            "1" => (ChecklistVerdict.Ok, "Diagnóstico obligatorio (Required). Es el mínimo realista en Windows 11 Pro/Home."),
            "0" => (ChecklistVerdict.Info, "AllowTelemetry=0 no es efectivo en Home/Pro: Windows lo trata como Required (1). Security/Enterprise sí pueden Security (0)."),
            "2" or "3" => (ChecklistVerdict.Gap, "Datos opcionales/completos activos. Bájalo a Obligatorio en Configuración > Privacidad > Diagnósticos."),
            "(missing)" => (ChecklistVerdict.Gap, "Sin política. En consumo suele quedar Optional. Objetivo: Required (1), no 'cero'."),
            _ => (ChecklistVerdict.Unknown, "Valor de telemetría no reconocido.")
        };

        return new ChecklistResult(item.Id, item.Section, item.Title, verdict, actual, "1 (Required)", item.HowTo, item.Policy, note, item.SettingsUri);
    }

    private static ChecklistResult Registry(ChecklistDefinition item, IRegistryReader registry)
    {
        if (item.Hive is null || item.Path is null || item.Name is null)
        {
            return Unknown(item, "detect incompleto");
        }

        object? raw;
        try
        {
            raw = registry.GetValue(item.Hive, item.Path, item.Name);
        }
        catch (UnauthorizedAccessException)
        {
            return new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Unknown, "denied", item.DesiredEquals ?? "", item.HowTo, item.Policy, "Registro denegado (a menudo HKLM sin admin).", item.SettingsUri);
        }

        var actual = Format(raw);
        var desired = item.DesiredEquals ?? "";
        var match = desired.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(actual, part, StringComparison.OrdinalIgnoreCase));

        if (item.Policy == ChecklistPolicy.NeverDisable)
        {
            if (!string.IsNullOrEmpty(desired) && !match)
            {
                return new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Gap, actual, desired, item.HowTo, item.Policy, "Anti-patrón: este valor no debería cambiarse en un baseline.", item.SettingsUri);
            }

            return new ChecklistResult(item.Id, item.Section, item.Title, match ? ChecklistVerdict.Ok : ChecklistVerdict.Info, actual, string.IsNullOrEmpty(desired) ? "no desactivar" : desired, item.HowTo, item.Policy, item.Description, item.SettingsUri);
        }

        if (item.Policy == ChecklistPolicy.Info)
        {
            return new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Info, actual, desired, item.HowTo, item.Policy, item.Description, item.SettingsUri);
        }

        return new ChecklistResult(
            item.Id, item.Section, item.Title,
            match ? ChecklistVerdict.Ok : ChecklistVerdict.Gap,
            actual,
            desired,
            item.HowTo,
            item.Policy,
            match ? "Coincide." : item.Description,
            item.SettingsUri);
    }

    private static ChecklistResult ProcessItem(ChecklistDefinition item)
    {
        var proc = item.ProcessName ?? "";
        var running = Process.GetProcessesByName(proc.Replace(".exe", "", StringComparison.OrdinalIgnoreCase)).Length > 0;
        var note = item.Policy == ChecklistPolicy.NeverDisable
            ? (running ? "En ejecución. No matar: Windows lo relanza y no es un 'boost'." : "No está en ejecución ahora. No hay que forzarlo.")
            : item.Description;
        return new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Info, running ? "running" : "not-running", "info", item.HowTo, item.Policy, note, item.SettingsUri);
    }

    private static ChecklistResult ServiceItem(ChecklistDefinition item, IRegistryReader registry)
    {
        var svc = item.ServiceName ?? item.Name;
        if (string.IsNullOrWhiteSpace(svc))
        {
            return Unknown(item, "servicio sin nombre");
        }

        var start = registry.GetValue("HKLM", $@"SYSTEM\CurrentControlSet\Services\{svc}", "Start");
        var actual = Format(start);
        var decoded = actual switch
        {
            "2" => "Auto (2)",
            "3" => "Manual (3)",
            "4" => "Disabled (4)",
            _ => actual
        };

        if (item.Policy == ChecklistPolicy.NeverDisable)
        {
            var note = actual == "4"
                ? "Servicio deshabilitado. No es recomendación de WindowsLab (Defender/Search/SysMain/DiagTrack)."
                : "Presente. No deshabilitar como 'optimización'.";
            var verdict = actual == "4" && string.Equals(svc, "WinDefend", StringComparison.OrdinalIgnoreCase)
                ? ChecklistVerdict.Gap
                : ChecklistVerdict.Info;
            return new ChecklistResult(item.Id, item.Section, item.Title, verdict, decoded, "no deshabilitar", item.HowTo, item.Policy, note, item.SettingsUri);
        }

        var match = (item.DesiredEquals ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(actual, part, StringComparison.OrdinalIgnoreCase));
        return new ChecklistResult(item.Id, item.Section, item.Title, match ? ChecklistVerdict.Ok : ChecklistVerdict.Gap, decoded, item.DesiredEquals ?? "", item.HowTo, item.Policy, item.Description, item.SettingsUri);
    }

    private static ChecklistResult Tool(ChecklistDefinition item, MachineInventory inventory)
    {
        var tool = item.ToolId ?? "";
        var present = inventory.Toolchain.Present.Contains(tool, StringComparer.OrdinalIgnoreCase);
        return new ChecklistResult(
            item.Id, item.Section, item.Title,
            present ? ChecklistVerdict.Ok : ChecklistVerdict.Info,
            present ? "instalado" : "no en PATH",
            "opcional",
            item.HowTo,
            item.Policy,
            present ? "Detectado en PATH." : item.Description,
            item.SettingsUri);
    }

    private static ChecklistResult Defender(ChecklistDefinition item, MachineInventory inventory)
    {
        var on = inventory.Security.DefenderEnabled;
        if (on is null)
        {
            return new ChecklistResult(item.Id, item.Section, item.Title, ChecklistVerdict.Unknown, "unknown", "on", item.HowTo, item.Policy, "WMI Defender denegado (a menudo hace falta admin).", item.SettingsUri);
        }

        return new ChecklistResult(
            item.Id, item.Section, item.Title,
            on.Value ? ChecklistVerdict.Ok : ChecklistVerdict.Gap,
            on.Value ? "on" : "off",
            "on",
            item.HowTo,
            item.Policy,
            on.Value ? "Defender activo." : "No desactivar Defender. Reactívalo en Seguridad de Windows.",
            item.SettingsUri);
    }

    private static ChecklistResult Firewall(ChecklistDefinition item, MachineInventory inventory)
    {
        var on = inventory.Security.FirewallEnabled;
        if (on is null)
        {
            return Unknown(item, "firewall unknown");
        }

        return new ChecklistResult(
            item.Id, item.Section, item.Title,
            on.Value ? ChecklistVerdict.Ok : ChecklistVerdict.Gap,
            on.Value ? "on" : "off",
            "on",
            item.HowTo,
            item.Policy,
            on.Value ? "Firewall de perfil estándar activado." : "El firewall no debería apagarse en un baseline.",
            item.SettingsUri);
    }

    private static ChecklistResult Disk(ChecklistDefinition item, MachineInventory inventory)
    {
        var c = inventory.Volumes.FirstOrDefault(v => v.Root.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
        if (c is null)
        {
            return Unknown(item, "sin C:");
        }

        var gb = c.FreeBytes / (1024d * 1024 * 1024);
        var ok = gb >= 20;
        return new ChecklistResult(
            item.Id, item.Section, item.Title,
            ok ? ChecklistVerdict.Ok : ChecklistVerdict.Gap,
            $"{gb:0.0} GB",
            ">= 20 GB",
            item.HowTo,
            item.Policy,
            ok ? "Espacio razonable en C:." : "C: bajo. Restaura/puntos de restauración pueden fallar.",
            item.SettingsUri);
    }

    private static ChecklistResult Unknown(ChecklistDefinition item, string note) =>
        new(item.Id, item.Section, item.Title, ChecklistVerdict.Unknown, "unknown", item.DesiredEquals ?? "", item.HowTo, item.Policy, note, item.SettingsUri);

    private static string Format(object? value) => value switch
    {
        null => "(missing)",
        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        uint u => u.ToString(System.Globalization.CultureInfo.InvariantCulture),
        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };
}

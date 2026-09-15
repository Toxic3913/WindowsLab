using System.Text.Json;
using WindowsLab.Core;

namespace WindowsLab.Applications;

public static class ApplicationCatalogLoader
{
    public static IReadOnlyList<ApplicationDefinition> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        var list = new List<ApplicationDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            list.AddRange(LoadFile(file));
        }

        return list
            .OrderBy(a => a.Category, StringComparer.Ordinal)
            .ThenBy(a => a.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ApplicationDefinition> LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return doc.RootElement.EnumerateArray().Select(ParseOne).ToArray();
        }

        return [ParseOne(doc.RootElement)];
    }

    private static ApplicationDefinition ParseOne(JsonElement el)
    {
        var axesEl = el.GetProperty("axes");
        var axes = new AppAxes(
            ReadDouble(axesEl, "privacy"),
            ReadDouble(axesEl, "telemetry"),
            ReadDouble(axesEl, "security"),
            ReadDouble(axesEl, "performance"),
            ReadDouble(axesEl, "ecosystem"));

        return new ApplicationDefinition(
            ReadString(el, "id") ?? throw new InvalidDataException("application id required"),
            ReadString(el, "title") ?? "",
            ReadString(el, "description") ?? "",
            ReadString(el, "wingetId") ?? throw new InvalidDataException("wingetId required"),
            ReadString(el, "category") ?? "general",
            ParseEvidence(ReadString(el, "evidence")),
            ReadStringArray(el, "evidenceRefs"),
            ParseRisk(ReadString(el, "risk")),
            axes,
            ReadStringArray(el, "profiles"),
            ParseScope(ReadString(el, "scopePreference")),
            ReadStringArray(el, "detectNames"));
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static double ReadDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || !p.TryGetDouble(out var v))
        {
            throw new InvalidDataException($"axes.{name} required");
        }

        return Math.Clamp(v, 0, 1);
    }

    private static string[] ReadStringArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return p.EnumerateArray()
            .Select(x => x.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToArray();
    }

    private static EvidenceGrade ParseEvidence(string? value) => value?.ToUpperInvariant() switch
    {
        "OFFICIAL" => EvidenceGrade.Official,
        "STRONG" => EvidenceGrade.Strong,
        "COMMUNITY" => EvidenceGrade.Community,
        "EXPERIMENTAL" => EvidenceGrade.Experimental,
        _ => EvidenceGrade.Unknown
    };

    private static RiskLevel ParseRisk(string? value) => value?.ToUpperInvariant() switch
    {
        "MEDIUM" => RiskLevel.Medium,
        "HIGH" => RiskLevel.High,
        "CRITICAL" => RiskLevel.Critical,
        _ => RiskLevel.Low
    };

    private static AppInstallScope ParseScope(string? value) => value?.ToLowerInvariant() switch
    {
        "machine" => AppInstallScope.Machine,
        "any" => AppInstallScope.Any,
        _ => AppInstallScope.User
    };
}

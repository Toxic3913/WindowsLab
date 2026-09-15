using System.Text.Json;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public static class TweakCatalogLoader
{
    public static IReadOnlyList<TweakDefinition> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        var list = new List<TweakDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            list.AddRange(LoadFile(file));
        }

        return list
            .OrderBy(t => t.Category, StringComparer.Ordinal)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<TweakDefinition> LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return doc.RootElement.EnumerateArray().Select(ParseOne).ToArray();
        }

        return [ParseOne(doc.RootElement)];
    }

    private static TweakDefinition ParseOne(JsonElement el)
    {
        var detect = el.GetProperty("detect");
        var desired = el.GetProperty("desired").GetProperty("equals");
        var desiredText = desired.ValueKind switch
        {
            JsonValueKind.Number => desired.GetRawText(),
            JsonValueKind.String => desired.GetString() ?? "",
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Null => "",
            _ => desired.GetRawText()
        };

        var ops = new List<TweakOp>();
        if (el.TryGetProperty("ops", out var opsEl) && opsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var op in opsEl.EnumerateArray())
            {
                ops.Add(new TweakOp(
                    ReadString(op, "kind") ?? "registry",
                    ReadString(op, "hive"),
                    ReadString(op, "path"),
                    ReadString(op, "name"),
                    ReadString(op, "valueKind"),
                    ReadString(op, "value")));
            }
        }

        return new TweakDefinition(
            ReadString(el, "id") ?? throw new InvalidDataException("tweak id required"),
            ReadString(el, "title") ?? "",
            ReadString(el, "description") ?? "",
            ReadString(el, "category") ?? "general",
            ParseRisk(ReadString(el, "risk")),
            ParseEvidence(ReadString(el, "evidence")),
            ReadStringArray(el, "evidenceRefs"),
            el.TryGetProperty("minBuild", out var mb) && mb.TryGetInt32(out var minB) ? minB : 22000,
            ReadStringArray(el, "editions"),
            ReadStringArray(el, "profiles"),
            ReadStringArray(el, "requires"),
            ReadStringArray(el, "conflicts"),
            el.TryGetProperty("requiresReboot", out var rb) && rb.ValueKind == JsonValueKind.True,
            el.TryGetProperty("affectsSecurity", out var ase) && ase.ValueKind == JsonValueKind.True,
            el.TryGetProperty("affectsUpdates", out var au) && au.ValueKind == JsonValueKind.True,
            el.TryGetProperty("affectsCompatibility", out var ac) && ac.ValueKind == JsonValueKind.True,
            new RegistryDetect(
                ReadString(detect, "hive") ?? "HKCU",
                ReadString(detect, "path") ?? "",
                ReadString(detect, "name") ?? "",
                ReadString(detect, "valueKind") ?? "DWord"),
            desiredText,
            ops);
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string[] ReadStringArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return p.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray();
    }

    private static RiskLevel ParseRisk(string? value) => value?.ToUpperInvariant() switch
    {
        "LOW" => RiskLevel.Low,
        "MEDIUM" => RiskLevel.Medium,
        "HIGH" => RiskLevel.High,
        "CRITICAL" => RiskLevel.Critical,
        _ => RiskLevel.Medium
    };

    private static EvidenceGrade ParseEvidence(string? value) => value?.ToUpperInvariant() switch
    {
        "OFFICIAL" => EvidenceGrade.Official,
        "STRONG" => EvidenceGrade.Strong,
        "COMMUNITY" => EvidenceGrade.Community,
        "EXPERIMENTAL" => EvidenceGrade.Experimental,
        _ => EvidenceGrade.Unknown
    };
}

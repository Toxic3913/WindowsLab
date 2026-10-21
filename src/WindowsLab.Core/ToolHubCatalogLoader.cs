using System.Text.Json;

namespace WindowsLab.Core;

public static class ToolHubCatalogLoader
{
    public static IReadOnlyList<ToolHubEntry> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        var list = new List<ToolHubEntry>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            list.AddRange(LoadFile(file));
        }

        return list
            .OrderBy(e => GroupOrder(e.Group))
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<ToolHubEntry> LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return doc.RootElement.EnumerateArray().Select(ParseOne).ToArray();
        }

        return [ParseOne(doc.RootElement)];
    }

    private static int GroupOrder(string group) => group.ToLowerInvariant() switch
    {
        "desktop" => 0,
        "performance" => 1,
        "network" => 2,
        "accessibility" => 3,
        _ => 9
    };

    private static ToolHubEntry ParseOne(JsonElement el)
    {
        var id = ReadString(el, "id") ?? throw new InvalidDataException("tool-hub id required");
        var kind = ParseKind(ReadString(el, "kind"));
        return new ToolHubEntry(
            id,
            ReadString(el, "title") ?? id,
            ReadString(el, "group") ?? "desktop",
            ReadString(el, "summary") ?? "",
            ReadString(el, "summaryEn") ?? ReadString(el, "summary") ?? "",
            ReadString(el, "improves") ?? "",
            ReadString(el, "improvesEn") ?? ReadString(el, "improves") ?? "",
            kind,
            ReadString(el, "catalogAppId"),
            ReadString(el, "url"),
            ReadString(el, "steamAppId"),
            ReadString(el, "settingsUri"),
            ParseEvidence(ReadString(el, "evidence")),
            ReadString(el, "riskNote") ?? "",
            ReadString(el, "riskNoteEn") ?? ReadString(el, "riskNote") ?? "",
            ReadStringArray(el, "launchExeHints"));
    }

    private static ToolHubKind ParseKind(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "winget" => ToolHubKind.Winget,
        "externalurl" or "external" or "url" => ToolHubKind.ExternalUrl,
        "steam" => ToolHubKind.Steam,
        "windowssettings" or "settings" => ToolHubKind.WindowsSettings,
        "docsonly" or "docs" => ToolHubKind.DocsOnly,
        _ => throw new InvalidDataException($"Unknown tool-hub kind: {raw}")
    };

    private static EvidenceGrade ParseEvidence(string? raw) => (raw ?? "").Trim().ToUpperInvariant() switch
    {
        "OFFICIAL" => EvidenceGrade.Official,
        "STRONG" => EvidenceGrade.Strong,
        "COMMUNITY" => EvidenceGrade.Community,
        "EXPERIMENTAL" => EvidenceGrade.Experimental,
        "UNKNOWN" => EvidenceGrade.Unknown,
        _ => EvidenceGrade.Unknown
    };

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string[] ReadStringArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return p.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }
}

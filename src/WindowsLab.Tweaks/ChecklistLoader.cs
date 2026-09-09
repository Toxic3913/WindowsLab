using System.Text.Json;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public static class ChecklistLoader
{
    public static IReadOnlyList<ChecklistDefinition> LoadDirectory(string directory)
    {
        var list = new List<ChecklistDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            list.AddRange(LoadFile(file));
        }

        return list.OrderBy(c => c.Order).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<ChecklistDefinition> LoadFile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(Parse).ToArray();
        }

        return [Parse(root)];
    }

    private static ChecklistDefinition Parse(JsonElement el)
    {
        var detect = el.GetProperty("detect");
        var desired = el.TryGetProperty("desired", out var d) && d.TryGetProperty("equals", out var eq)
            ? EqText(eq)
            : null;
        var policy = (Read(el, "policy") ?? "info").ToUpperInvariant() switch
        {
            "RECOMMEND" => ChecklistPolicy.Recommend,
            "NEVER" or "NEVERDISABLE" => ChecklistPolicy.NeverDisable,
            _ => ChecklistPolicy.Info
        };

        return new ChecklistDefinition(
            Read(el, "id") ?? throw new InvalidDataException("checklist id"),
            Read(el, "section") ?? "general",
            Read(el, "title") ?? "",
            Read(el, "description") ?? "",
            Read(el, "howTo") ?? "",
            policy,
            Read(detect, "kind") ?? "manual",
            ReadInt(el, "order"),
            Read(detect, "hive"),
            Read(detect, "path"),
            Read(detect, "name"),
            Read(detect, "process"),
            Read(detect, "service"),
            Read(detect, "tool"),
            desired);
    }

    private static string? Read(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int ReadInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.TryGetInt32(out var n) ? n : 999;

    private static string EqText(JsonElement desired) => desired.ValueKind switch
    {
        JsonValueKind.Number => desired.GetRawText(),
        JsonValueKind.String => desired.GetString() ?? "",
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        _ => desired.GetRawText()
    };
}

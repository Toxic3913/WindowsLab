using System.Text.Json;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public static class PresetLoader
{
    public static IReadOnlyList<PresetDefinition> LoadDirectory(string directory)
    {
        var list = new List<PresetDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            list.AddRange(LoadFile(file));
        }

        return list.OrderBy(p => p.Order).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<PresetDefinition> LoadFile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(Parse).ToArray();
        }

        return [Parse(root)];
    }

    private static PresetDefinition Parse(JsonElement el)
    {
        var open = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (el.TryGetProperty("openById", out var openEl) && openEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in openEl.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { } uri)
                {
                    open[p.Name] = uri;
                }
            }
        }

        var steps = new List<PresetStep>();
        if (el.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in stepsEl.EnumerateArray())
            {
                steps.Add(new PresetStep(
                    Read(s, "title") ?? "",
                    Read(s, "howTo") ?? "",
                    Read(s, "settingsUri")));
            }
        }

        return new PresetDefinition(
            Read(el, "id") ?? throw new InvalidDataException("preset id"),
            Read(el, "title") ?? "",
            Read(el, "summary") ?? "",
            el.TryGetProperty("order", out var o) && o.TryGetInt32(out var n) ? n : 999,
            Read(el, "settingsUri"),
            el.TryGetProperty("custom", out var c) && c.ValueKind == JsonValueKind.True,
            ReadArray(el, "tweakIds"),
            ReadArray(el, "checklistIds"),
            ReadArray(el, "applicationIds"),
            steps,
            open);
    }

    private static string? Read(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string[] ReadArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return p.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray();
    }
}

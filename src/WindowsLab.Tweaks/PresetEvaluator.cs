using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public static class PresetEvaluator
{
    public static readonly IReadOnlyDictionary<string, string> DefaultOpenById =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gaming.game-mode-on"] = "ms-settings:gaming-gamemode",
            ["gaming.game-dvr-off"] = "ms-settings:gaming-gamedvr",
            ["gaming.allow-game-dvr-off"] = "ms-settings:gaming-gamedvr",
            ["gaming.hags-on"] = "ms-settings:display-advancedgraphics",
            ["gaming.gamebar-tips-off"] = "ms-settings:gaming-gamebar",
            ["power.best-performance"] = "ms-settings:powersleep",
            ["desktop.visual-fx-performance"] = "SystemPropertiesPerformance.exe",
            ["taskbar.widgets-off"] = "ms-settings:taskbar",
            ["taskbar.search-icon"] = "ms-settings:taskbar",
            ["taskbar.hide-task-view"] = "ms-settings:taskbar",
            ["keyboard.stickykeys-hotkey-off"] = "ms-settings:easeofaccess-keyboard",
            ["privacy.advertising-id-off"] = "ms-settings:privacy-general",
            ["privacy.tailored-experiences-off"] = "ms-settings:privacy-feedback",
            ["privacy.allow-telemetry-required"] = "ms-settings:privacy-feedback",
            ["privacy.search-highlights-off"] = "ms-settings:search",
            ["privacy.start-suggestions-off"] = "ms-settings:personalization-start",
            ["network.delivery-opt-lan"] = "ms-settings:delivery-optimization",
            ["developer.long-paths"] = "ms-settings:developers"
        };

    public static IReadOnlyList<PresetEvaluation> EvaluateAll(
        IReadOnlyList<PresetDefinition> presets,
        IReadOnlyList<TweakDefinition> catalog,
        IReadOnlyList<TweakDetection> detections,
        IReadOnlyList<ChecklistResult> checklist) =>
        presets.Select(p => Evaluate(p, catalog, detections, checklist)).ToArray();

    public static PresetEvaluation Evaluate(
        PresetDefinition preset,
        IReadOnlyList<TweakDefinition> catalog,
        IReadOnlyList<TweakDetection> detections,
        IReadOnlyList<ChecklistResult> checklist)
    {
        var tweakById = catalog.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        var detById = detections.ToDictionary(d => d.TweakId, StringComparer.OrdinalIgnoreCase);
        var checkById = checklist.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var items = new List<PresetItemStatus>();

        var tweakIds = preset.IsCustom
            ? catalog.Where(t => t.Evidence is not EvidenceGrade.Unknown and not EvidenceGrade.Experimental)
                .Select(t => t.Id)
            : preset.TweakIds;

        foreach (var id in tweakIds)
        {
            if (!tweakById.TryGetValue(id, out var tweak))
            {
                items.Add(Row(id, "tweak", id, false, "?", "(missing in catalog)", "", "", Open(preset, id)));
                continue;
            }

            detById.TryGetValue(id, out var det);
            var ready = det?.MatchesDesired == true;
            items.Add(Row(
                id,
                "tweak",
                tweak.Title,
                ready,
                ready ? "OK" : "FALTA",
                det?.ActualDisplay ?? "(unknown)",
                det?.DesiredDisplay ?? tweak.DesiredEquals,
                tweak.Description,
                Open(preset, id)));
        }

        foreach (var id in preset.ChecklistIds)
        {
            if (!checkById.TryGetValue(id, out var check))
            {
                items.Add(Row(id, "checklist", id, false, "?", "(missing)", "", "", Open(preset, id)));
                continue;
            }

            var ready = check.Verdict == ChecklistVerdict.Ok;
            var estado = check.Verdict switch
            {
                ChecklistVerdict.Ok => "OK",
                ChecklistVerdict.Gap => "FALTA",
                ChecklistVerdict.Unknown => "?",
                _ => "INFO"
            };
            items.Add(Row(id, "checklist", check.Title, ready, estado, check.Actual, check.Desired, check.HowTo, Open(preset, id) ?? GuessUri(check.HowTo)));
        }

        foreach (var step in preset.Steps)
        {
            items.Add(new PresetItemStatus(
                "step:" + step.Title,
                "step",
                step.Title,
                false,
                "MANUAL",
                "Windows Settings",
                "",
                step.HowTo,
                step.SettingsUri ?? preset.SettingsUri));
        }

        var counted = items.Where(i => i.Kind != "step" && i.Estado is "OK" or "FALTA").ToArray();
        var readyCount = counted.Count(i => i.Ready);
        var gapCount = counted.Count(i => !i.Ready);
        return new PresetEvaluation(preset, items, readyCount, gapCount, counted.Length);
    }

    private static PresetItemStatus Row(
        string id, string kind, string title, bool ready, string estado,
        string actual, string desired, string howTo, string? uri) =>
        new(id, kind, title, ready, estado, actual, desired, howTo, uri);

    private static string? Open(PresetDefinition preset, string id)
    {
        if (preset.OpenById.TryGetValue(id, out var uri))
        {
            return uri;
        }

        return DefaultOpenById.TryGetValue(id, out var fallback) ? fallback : null;
    }

    private static string? GuessUri(string howTo) =>
        howTo.Contains("Privacidad", StringComparison.OrdinalIgnoreCase) ? "ms-settings:privacy"
        : howTo.Contains("Barra", StringComparison.OrdinalIgnoreCase) ? "ms-settings:taskbar"
        : howTo.Contains("Energ", StringComparison.OrdinalIgnoreCase) ? "ms-settings:powersleep"
        : null;
}

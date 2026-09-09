namespace WindowsLab.Core;

public sealed record PresetDefinition(
    string Id,
    string Title,
    string Summary,
    int Order,
    string? SettingsUri,
    bool IsCustom,
    IReadOnlyList<string> TweakIds,
    IReadOnlyList<string> ChecklistIds,
    IReadOnlyList<PresetStep> Steps,
    IReadOnlyDictionary<string, string> OpenById);

public sealed record PresetStep(
    string Title,
    string HowTo,
    string? SettingsUri);

public sealed record PresetItemStatus(
    string Id,
    string Kind,
    string Title,
    bool Ready,
    string Estado,
    string Actual,
    string Desired,
    string HowTo,
    string? SettingsUri);

public sealed record PresetEvaluation(
    PresetDefinition Preset,
    IReadOnlyList<PresetItemStatus> Items,
    int ReadyCount,
    int GapCount,
    int Total);

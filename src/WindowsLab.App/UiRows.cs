using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public sealed class PerfProcessRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string GroupLabel { get; init; } = "";
    public ProcessGroup Group { get; init; }
    public string CpuText { get; init; } = "";
    public string WorkingSetText { get; init; } = "";
    public string PrivateText { get; init; } = "";
    public string Path { get; init; } = "";
}

public sealed class PresetPick
{
    public required PresetEvaluation Eval { get; init; }

    public string Label => Eval.Total == 0
        ? Eval.Preset.Title
        : $"{Eval.Preset.Title}  ({Eval.ReadyCount}/{Eval.Total})";
}

public sealed class TweakRow
{
    public bool IsSelected { get; set; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Risk { get; init; }
    public required string Evidence { get; init; }
    public string? Actual { get; init; }
    public string? Desired { get; init; }
    public bool? Match { get; init; }
    public string? Status { get; init; }
    public string MatchLabel => Match is true ? "✓" : Match is false ? "—" : "?";
    public bool IsRecommended { get; init; }
}

public sealed class TweakCategoryGroup
{
    public required string Category { get; init; }
    public required string Header { get; init; }
    public required IReadOnlyList<TweakRow> Rows { get; init; }
}

public sealed class AppRow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required double Score { get; init; }
    public required string Evidence { get; init; }
    public required string InstalledLabel { get; init; }
    public required string Why { get; init; }
    public required string WingetId { get; init; }
    public double Privacy { get; init; }
    public double Telemetry { get; init; }
    public double Security { get; init; }
    public double Performance { get; init; }
    public double Ecosystem { get; init; }
}

public sealed class ChecklistRow
{
    public required string Id { get; init; }
    public required string Estado { get; init; }
    public required string Section { get; init; }
    public required string Title { get; init; }
    public required string HowTo { get; init; }
    public required string? SettingsUri { get; init; }
    public required ChecklistVerdict Verdict { get; init; }
    public required ChecklistPolicy Policy { get; init; }
    public bool CanActivate => Policy != ChecklistPolicy.NeverDisable;
}

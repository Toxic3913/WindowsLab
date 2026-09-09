namespace WindowsLab.Core;

public enum ChecklistPolicy
{
    Recommend,
    Info,
    NeverDisable
}

public enum ChecklistVerdict
{
    Ok,
    Gap,
    Info,
    Unknown
}

public sealed record ChecklistDefinition(
    string Id,
    string Section,
    string Title,
    string Description,
    string HowTo,
    ChecklistPolicy Policy,
    string DetectKind,
    int Order,
    string? Hive,
    string? Path,
    string? Name,
    string? ProcessName,
    string? ServiceName,
    string? ToolId,
    string? DesiredEquals);

public sealed record ChecklistResult(
    string Id,
    string Section,
    string Title,
    ChecklistVerdict Verdict,
    string Actual,
    string Desired,
    string HowTo,
    ChecklistPolicy Policy,
    string Note);

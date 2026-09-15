using WindowsLab.Core;

namespace WindowsLab.Recommendations;

public sealed record AppRecommendation(
    string AppId,
    string Title,
    string Category,
    string WingetId,
    double Score,
    string Why,
    EvidenceGrade Evidence,
    RiskLevel Risk,
    bool Installed,
    AppAxes Axes,
    IReadOnlyDictionary<string, double> ProfileWeights);

public static class AppRecommendationEngine
{
    private static readonly Dictionary<UserProfile, IReadOnlyDictionary<string, double>> Weights =
        new()
        {
            [UserProfile.Balanced] = AxisMap(0.25, 0.20, 0.25, 0.15, 0.15),
            [UserProfile.Developer] = AxisMap(0.20, 0.15, 0.25, 0.15, 0.25),
            [UserProfile.Gaming] = AxisMap(0.10, 0.10, 0.20, 0.35, 0.25),
            [UserProfile.Virtualization] = AxisMap(0.20, 0.20, 0.30, 0.15, 0.15)
        };

    public static IReadOnlyDictionary<string, double> WeightsFor(UserProfile profile) =>
        Weights.TryGetValue(profile, out var w) ? w : Weights[UserProfile.Balanced];

    public static IReadOnlyList<AppRecommendation> Rank(
        IReadOnlyList<ApplicationDefinition> catalog,
        UserProfile profile,
        IReadOnlySet<string>? installedAppIds = null,
        string? categoryFilter = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var selected = ProfileName(profile);
        var weights = WeightsFor(profile);
        var list = new List<AppRecommendation>();

        foreach (var app in catalog)
        {
            if (app.Evidence is EvidenceGrade.Unknown or EvidenceGrade.Experimental)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter)
                && !string.Equals(app.Category, categoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (app.Profiles.Count > 0
                && !app.Profiles.Any(p => string.Equals(p, selected, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var evidenceW = app.Evidence switch
            {
                EvidenceGrade.Official => 1.0,
                EvidenceGrade.Strong => 0.8,
                EvidenceGrade.Community => 0.4,
                _ => 0
            };
            if (evidenceW <= 0)
            {
                continue;
            }

            var riskPenalty = app.Risk switch
            {
                RiskLevel.High => 0.5,
                RiskLevel.Critical => 1.0,
                RiskLevel.Medium => 0.15,
                _ => 0
            };

            var axisScore =
                weights["privacy"] * app.Axes.Privacy
                + weights["telemetry"] * app.Axes.Telemetry
                + weights["security"] * app.Axes.Security
                + weights["performance"] * app.Axes.Performance
                + weights["ecosystem"] * app.Axes.Ecosystem;

            var score = (axisScore * evidenceW) - riskPenalty;
            if (score <= 0)
            {
                continue;
            }

            var installed = installedAppIds is not null
                && installedAppIds.Contains(app.Id);
            var topAxes = weights
                .OrderByDescending(kv => kv.Value * AxisValue(app.Axes, kv.Key))
                .Take(2)
                .Select(kv => kv.Key)
                .ToArray();

            list.Add(new AppRecommendation(
                app.Id,
                app.Title,
                app.Category,
                app.WingetId,
                Math.Round(score, 3),
                Why(selected, topAxes, installed),
                app.Evidence,
                app.Risk,
                installed,
                app.Axes,
                weights));
        }

        return list
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.AppId, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, double> AxisMap(
        double privacy, double telemetry, double security, double performance, double ecosystem) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["privacy"] = privacy,
            ["telemetry"] = telemetry,
            ["security"] = security,
            ["performance"] = performance,
            ["ecosystem"] = ecosystem
        };

    private static double AxisValue(AppAxes axes, string name) => name.ToLowerInvariant() switch
    {
        "privacy" => axes.Privacy,
        "telemetry" => axes.Telemetry,
        "security" => axes.Security,
        "performance" => axes.Performance,
        "ecosystem" => axes.Ecosystem,
        _ => 0
    };

    private static string ProfileName(UserProfile profile) => profile switch
    {
        UserProfile.Developer => "developer",
        UserProfile.Gaming => "gaming",
        UserProfile.Virtualization => "virtualization",
        _ => "balanced"
    };

    private static string Why(string profileName, string[] topAxes, bool installed)
    {
        var axes = topAxes.Length == 0 ? "axes" : string.Join("+", topAxes);
        var state = installed ? "ya instalado" : "no instalado";
        return $"Score multi-eje ({axes}) para perfil {profileName}; {state}. Ranking relativo al perfil (sin ganador universal).";
    }
}

using WindowsLab.Core;

namespace WindowsLab.Recommendations;

public sealed record Recommendation(
    string TweakId,
    string Title,
    string Category,
    double Score,
    string Why,
    RiskLevel Risk,
    EvidenceGrade Evidence,
    bool MatchesDesired);

public static class RecommendationEngine
{
    public static IReadOnlyList<Recommendation> Rank(
        IReadOnlyList<TweakDefinition> catalog,
        IReadOnlyList<TweakDetection> detections,
        UserProfile profile,
        MachineInventory inventory)
    {
        var byId = detections.ToDictionary(d => d.TweakId, StringComparer.OrdinalIgnoreCase);
        var selected = ProfileName(profile);
        var list = new List<Recommendation>();

        foreach (var tweak in catalog)
        {
            if (tweak.Evidence is EvidenceGrade.Unknown or EvidenceGrade.Experimental)
            {
                continue;
            }

            if (tweak.MinBuild > inventory.Os.Build)
            {
                continue;
            }

            if (!EditionOk(tweak.Editions, inventory.Os.EditionId))
            {
                continue;
            }

            if (!RequiresOk(tweak.Requires, inventory))
            {
                continue;
            }

            byId.TryGetValue(tweak.Id, out var detection);
            if (detection?.MatchesDesired == true)
            {
                continue;
            }

            var evidenceW = tweak.Evidence switch
            {
                EvidenceGrade.Official => 1.0,
                EvidenceGrade.Strong => 0.8,
                EvidenceGrade.Community => 0.4,
                _ => 0
            };
            var reverseW = tweak.Risk switch
            {
                RiskLevel.Low => 1.0,
                RiskLevel.Medium => 0.7,
                RiskLevel.High => 0.3,
                _ => 0
            };
            var profileMatch = tweak.Profiles.Count == 0
                || tweak.Profiles.Any(p => string.Equals(p, selected, StringComparison.OrdinalIgnoreCase))
                ? 1.0
                : 0.2;
            var riskPenalty = tweak.Risk switch
            {
                RiskLevel.High => 0.5,
                RiskLevel.Critical => 1.0,
                _ => 0
            };

            var score = (evidenceW * reverseW * profileMatch) - riskPenalty;
            if (score <= 0)
            {
                continue;
            }

            list.Add(new Recommendation(
                tweak.Id,
                tweak.Title,
                tweak.Category,
                Math.Round(score, 3),
                Why(tweak, selected, detection),
                tweak.Risk,
                tweak.Evidence,
                detection?.MatchesDesired == true));
        }

        return list
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.TweakId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ProfileName(UserProfile profile) => profile switch
    {
        UserProfile.Developer => "developer",
        UserProfile.Gaming => "gaming",
        UserProfile.Virtualization => "virtualization",
        _ => "balanced"
    };

    private static bool EditionOk(IReadOnlyList<string> editions, string editionId)
    {
        if (editions.Count == 0 || editions.Any(e => e == "*"))
        {
            return true;
        }

        return editions.Any(e => string.Equals(e, editionId, StringComparison.OrdinalIgnoreCase)
            || (e.Equals("Professional", StringComparison.OrdinalIgnoreCase)
                && editionId.Equals("Professional", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool RequiresOk(IReadOnlyList<string> requires, MachineInventory inventory)
    {
        foreach (var req in requires)
        {
            if (req.StartsWith("gpu.vendor=", StringComparison.OrdinalIgnoreCase))
            {
                var vendor = req["gpu.vendor=".Length..];
                if (!inventory.Gpus.Any(g => g.Vendor.Equals(vendor, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }
            else if (req.Equals("toolchain.git", StringComparison.OrdinalIgnoreCase)
                     && !inventory.Toolchain.Present.Contains("git", StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string Why(TweakDefinition tweak, string profile, TweakDetection? detection)
    {
        var state = detection is null
            ? "estado desconocido"
            : detection.MatchesDesired
                ? "ya coincide"
                : $"actual={detection.ActualDisplay}, desired={detection.DesiredDisplay}";
        return $"{tweak.Evidence} / {tweak.Risk}; perfil {profile}; {state}";
    }
}

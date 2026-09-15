using WindowsLab.Applications;
using WindowsLab.Audit;
using WindowsLab.Core;
using WindowsLab.Recommendations;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public sealed class LabSession
{
    public MachineInventory Inventory { get; }
    public IReadOnlyList<TweakDefinition> Catalog { get; }
    public IReadOnlyList<TweakDetection> Detections { get; }
    public IReadOnlyList<Recommendation> Recommendations { get; }
    public IReadOnlyList<ChecklistDefinition> Checklist { get; }
    public IReadOnlyList<ChecklistResult> ChecklistResults { get; }
    public IReadOnlyList<PresetDefinition> Presets { get; }
    public IReadOnlyList<PresetEvaluation> PresetEvals { get; }
    public IReadOnlyList<ApplicationDefinition> Applications { get; }
    public IReadOnlyList<AppRecommendation> AppRecommendations { get; }
    public UserProfile Profile { get; }

    public LabSession(UserProfile profile)
    {
        Profile = profile;
        Inventory = AuditRunner.Run();
        var dir = CatalogLocator.FindTweaksDirectory();
        Catalog = dir is null ? [] : TweakCatalogLoader.LoadDirectory(dir);
        Detections = TweakDetector.DetectAll(Catalog, new LiveRegistryReader());
        Recommendations = RecommendationEngine.Rank(Catalog, Detections, profile, Inventory);
        var checkDir = CatalogLocator.FindChecklistsDirectory();
        Checklist = checkDir is null ? [] : ChecklistLoader.LoadDirectory(checkDir);
        ChecklistResults = ChecklistEvaluator.Evaluate(Checklist, Inventory, new LiveRegistryReader());
        var presetDir = CatalogLocator.FindPresetsDirectory();
        Presets = presetDir is null ? [] : PresetLoader.LoadDirectory(presetDir);
        PresetEvals = PresetEvaluator.EvaluateAll(Presets, Catalog, Detections, ChecklistResults);
        var appsDir = CatalogLocator.FindApplicationsDirectory();
        Applications = appsDir is null ? [] : ApplicationCatalogLoader.LoadDirectory(appsDir);
        var facts = InstalledAppDetector.Scan();
        var installedIds = Applications
            .Where(a => InstalledAppDetector.IsInstalled(a, facts))
            .Select(a => a.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        AppRecommendations = AppRecommendationEngine.Rank(Applications, profile, installedIds);
    }

    public VolumeInventory? SystemVolume =>
        Inventory.Volumes.FirstOrDefault(v => v.Root.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
}

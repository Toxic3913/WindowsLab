using System.Text.Json;
using WindowsLab.Applications;
using WindowsLab.Audit;
using WindowsLab.Backup;
using WindowsLab.Core;
using WindowsLab.Recommendations;
using WindowsLab.Tweaks;

namespace WindowsLab.Cli;

public sealed class CliServices
{
    public Func<OsIdentity> ReadOs { get; init; } = OsIdentityReader.Read;
    public Func<MachineInventory> Audit { get; init; } = AuditRunner.Run;
    public Func<IReadOnlyList<TweakDefinition>> LoadCatalog { get; init; } = LoadCatalogDefault;
    public Func<IReadOnlyList<ChecklistDefinition>> LoadChecklists { get; init; } = LoadChecklistsDefault;
    public Func<IReadOnlyList<PresetDefinition>> LoadPresets { get; init; } = LoadPresetsDefault;
    public Func<IReadOnlyList<ApplicationDefinition>> LoadApplications { get; init; } = LoadApplicationsDefault;
    public Func<IRegistryReader> Registry { get; init; } = () => new LiveRegistryReader();
    public Func<string> WindowsIdentityName { get; init; } = () => System.Security.Principal.WindowsIdentity.GetCurrent().Name;

    private static IReadOnlyList<TweakDefinition> LoadCatalogDefault()
    {
        var dir = CatalogLocator.FindTweaksDirectory();
        return dir is null ? [] : TweakCatalogLoader.LoadDirectory(dir);
    }

    private static IReadOnlyList<ChecklistDefinition> LoadChecklistsDefault()
    {
        var dir = CatalogLocator.FindChecklistsDirectory();
        return dir is null ? [] : ChecklistLoader.LoadDirectory(dir);
    }

    private static IReadOnlyList<PresetDefinition> LoadPresetsDefault()
    {
        var dir = CatalogLocator.FindPresetsDirectory();
        return dir is null ? [] : PresetLoader.LoadDirectory(dir);
    }

    private static IReadOnlyList<ApplicationDefinition> LoadApplicationsDefault()
    {
        var dir = CatalogLocator.FindApplicationsDirectory();
        return dir is null ? [] : ApplicationCatalogLoader.LoadDirectory(dir);
    }
}

public static class CliApp
{
    public const int SchemaVersion = 1;

    public const string HelpText =
        """
        WindowsLab CLI — 1.0.0 (audit · stacks · apply · live · apps)
        Ejecutable: windowslab-cli.exe  (no confundir con WindowsLab.exe = GUI)

        Uso:
          windowslab-cli --help
          windowslab-cli audit
          windowslab-cli audit --os
          windowslab-cli audit --output json
          windowslab-cli live [--output json]
          windowslab-cli tweak list|detect|simulate <id>
          windowslab-cli tweak apply <id> --lab-apply [--dry-run]
          windowslab-cli tweak apply <id> --apply [--yes] [--i-am-on-lab-vm] [--i-accept-security-impact] [--dry-run]
          windowslab-cli tweak rollback --backup-id <id> [--i-am-on-lab-vm]
          windowslab-cli backup list [--output json]
          windowslab-cli recommend [--profile …]
          windowslab-cli app list [--category browser|tool]
          windowslab-cli app recommend [--profile …]
          windowslab-cli app install <id> --yes
          windowslab-cli checklist
          windowslab-cli preset list|show|simulate <id>
          windowslab-cli preset apply <id> --yes [--i-am-on-lab-vm] [--i-accept-security-impact]

        --lab-apply = HKCU LOW only (no Worker). --apply = system pipeline (UAC/Worker).
        System apply on host needs AllowSystemApply or --i-am-on-lab-vm (D020).
        Security-affecting tweaks (D021) also need --i-accept-security-impact.
        live = one-shot CPU/RAM/disk + top processes (D022).

        Código: D:\WindowsLab    Programa: C:\Program Files\WindowsLab
        """;

    public static int Run(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        Func<OsIdentity>? readOs = null,
        CliServices? services = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        services ??= new CliServices();
        if (readOs is not null)
        {
            services = new CliServices
            {
                ReadOs = readOs,
                Audit = services.Audit,
                LoadCatalog = services.LoadCatalog,
                LoadChecklists = services.LoadChecklists,
                LoadPresets = services.LoadPresets,
                LoadApplications = services.LoadApplications,
                Registry = services.Registry,
                WindowsIdentityName = services.WindowsIdentityName
            };
        }

        var json = HasFlag(args, "--output") && GetOption(args, "--output") is "json"
                   || HasFlag(args, "--format") && GetOption(args, "--format") is "json";

        if (args.Count == 0 || IsHelp(args))
        {
            stdout.WriteLine(HelpText);
            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "audit") && EqualsCmd(args, 1, "--os"))
        {
            var identity = services.ReadOs();
            if (json)
            {
                WriteJson(stdout, "audit --os", ExitCodes.Ok, identity);
            }
            else
            {
                WriteOs(stdout, identity);
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "audit"))
        {
            var inventory = services.Audit();
            if (json)
            {
                WriteJson(stdout, "audit", ExitCodes.Ok, inventory);
            }
            else
            {
                WriteInventory(stdout, inventory);
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "live"))
        {
            // Second sample improves CPU % after first warm-up tick.
            _ = LiveSystemReader.ReadDashboard(includeProcesses: true, processTopN: 25);
            Thread.Sleep(500);
            var dash = LiveSystemReader.ReadDashboard(includeProcesses: true, processTopN: 25);
            if (json)
            {
                WriteJson(stdout, "live", ExitCodes.Ok, dash);
            }
            else
            {
                WriteLive(stdout, dash);
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "tweak") && EqualsCmd(args, 1, "apply"))
        {
            var lab = HasFlag(args, "--lab-apply");
            var system = HasFlag(args, "--apply");
            if (!lab && !system)
            {
                stderr.WriteLine("Apply: usa --lab-apply (HKCU) o --apply (sistema/Worker). Sin flag: exit 13.");
                return ExitCodes.PolicyBlocked;
            }

            if (args.Count < 3 || args[2].StartsWith('-'))
            {
                stderr.WriteLine("Usage: windowslab-cli tweak apply <id> (--lab-apply|--apply) [--yes] [--dry-run] [--i-am-on-lab-vm]");
                return ExitCodes.Generic;
            }

            var id = args[2];
            var catalog = services.LoadCatalog();
            var tweak = catalog.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
            if (tweak is null)
            {
                stderr.WriteLine($"Tweak not found: {id}");
                return 10;
            }

            var dry = HasFlag(args, "--dry-run");
            var iAmLab = HasFlag(args, "--i-am-on-lab-vm");
            var settings = OperatorSettingsStore.Load();

            if (lab)
            {
                if (!TweakApplicator.IsLabEligible(tweak))
                {
                    stderr.WriteLine("Policy: tweak not eligible for lab apply (need HKCU LOW OFFICIAL/STRONG).");
                    return ExitCodes.PolicyBlocked;
                }

                var reader = services.Registry();
                IRegistryWriter writer = dry ? new DryRunRegistryWriter() : new LiveRegistryWriter();
                var result = TweakApplicator.Apply(tweak, reader, writer, dryRun: dry);
                string? backupPath = null;
                if (result.Backup is not null && !dry && result.Outcome is ApplyOutcome.Ok or ApplyOutcome.VerifyFailed)
                {
                    backupPath = TweakApplicator.PersistBackup(result.Backup);
                }

                if (json)
                {
                    WriteJson(stdout, "tweak apply", result.Succeeded ? ExitCodes.Ok : ExitCodes.Generic,
                        new { result.Outcome, result.Message, result.Backup, backupPath, dryRun = dry, mode = "lab" });
                }
                else
                {
                    stdout.WriteLine($"outcome: {result.Outcome}");
                    stdout.WriteLine($"message: {result.Message}");
                    if (backupPath is not null)
                    {
                        stdout.WriteLine($"backup: {backupPath}");
                    }
                }

                return result.Succeeded ? ExitCodes.Ok : ExitCodes.Generic;
            }

            // --apply system pipeline
            if (!HasFlag(args, "--yes") && !dry)
            {
                stderr.WriteLine("System apply requires --yes (and lab-vm opt-in if not on VM).");
                return ExitCodes.Generic;
            }

            if (tweak.AffectsSecurity && !HasFlag(args, "--i-accept-security-impact"))
            {
                stderr.WriteLine(
                    "Policy (D021): security-affecting tweak requires --i-accept-security-impact.");
                return ExitCodes.PolicyBlocked;
            }

            if (!TweakApplicator.IsApplyEligible(tweak) && !TweakApplicator.IsLabEligible(tweak))
            {
                stderr.WriteLine("Policy: tweak not eligible for system apply.");
                return ExitCodes.PolicyBlocked;
            }

            var catalogDir = CatalogLocator.FindTweaksDirectory()
                             ?? throw new DirectoryNotFoundException("catalog/tweaks not found");
            var job = SystemApplyEngine.ApplyAsync(
                [tweak],
                new FileBackupStore(),
                services.Registry(),
                settings,
                iAmLab,
                dry,
                catalogDir).GetAwaiter().GetResult();

            if (json)
            {
                WriteJson(stdout, "tweak apply", job.Succeeded ? ExitCodes.Ok : ExitCodes.Generic,
                    new { job.Succeeded, job.Message, job.BackupId, job.Results, mode = "system" });
            }
            else
            {
                stdout.WriteLine($"succeeded: {job.Succeeded}");
                stdout.WriteLine($"message: {job.Message}");
                if (job.BackupId is not null)
                {
                    stdout.WriteLine($"backupId: {job.BackupId}");
                }
            }

            return job.Succeeded ? ExitCodes.Ok : ExitCodes.Generic;
        }

        if (EqualsCmd(args, 0, "tweak") && EqualsCmd(args, 1, "rollback"))
        {
            var backupId = GetOption(args, "--backup-id");
            if (string.IsNullOrWhiteSpace(backupId))
            {
                stderr.WriteLine("Usage: windowslab-cli tweak rollback --backup-id <id> [--i-am-on-lab-vm]");
                return ExitCodes.Generic;
            }

            var store = new FileBackupStore();
            var result = SystemApplyEngine.RollbackAsync(
                backupId,
                store,
                OperatorSettingsStore.Load(),
                HasFlag(args, "--i-am-on-lab-vm")).GetAwaiter().GetResult();
            if (json)
            {
                WriteJson(stdout, "tweak rollback", result.Succeeded ? ExitCodes.Ok : ExitCodes.Generic, result);
            }
            else
            {
                stdout.WriteLine($"succeeded: {result.Succeeded}");
                stdout.WriteLine($"message: {result.Message}");
            }

            return result.Succeeded ? ExitCodes.Ok : ExitCodes.Generic;
        }

        if (EqualsCmd(args, 0, "backup") && EqualsCmd(args, 1, "list"))
        {
            var store = new FileBackupStore();
            var list = store.List();
            if (json)
            {
                WriteJson(stdout, "backup list", ExitCodes.Ok, list);
            }
            else
            {
                foreach (var b in list)
                {
                    stdout.WriteLine($"{b.BackupId}\t{b.CreatedUtc:u}\t{b.HighestRisk}\t{b.Reason}\t{string.Join(',', b.TweakIds)}");
                }
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "tweak") && EqualsCmd(args, 1, "list"))
        {
            var catalog = services.LoadCatalog();
            if (json)
            {
                WriteJson(stdout, "tweak list", ExitCodes.Ok, catalog.Select(t => new { t.Id, t.Title, t.Category, t.Risk, t.Evidence }));
            }
            else
            {
                foreach (var t in catalog)
                {
                    stdout.WriteLine($"{t.Id}\t{t.Risk}\t{t.Evidence}\t{t.Title}");
                }
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "tweak") && (EqualsCmd(args, 1, "detect") || EqualsCmd(args, 1, "simulate")))
        {
            if (args.Count < 3)
            {
                stderr.WriteLine("Usage: windowslab-cli tweak detect <id>");
                return ExitCodes.Generic;
            }

            var id = args[2];
            var catalog = services.LoadCatalog();
            var tweak = catalog.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
            if (tweak is null)
            {
                stderr.WriteLine($"Tweak not found: {id}");
                return 10;
            }

            var detection = TweakDetector.Detect(tweak, services.Registry());
            if (json)
            {
                WriteJson(stdout, "tweak detect", ExitCodes.Ok, new { detection, simulateOnly = true, wouldWrite = false });
            }
            else
            {
                stdout.WriteLine($"id: {detection.TweakId}");
                stdout.WriteLine($"status: {detection.Status}");
                stdout.WriteLine($"actual: {detection.ActualDisplay}");
                stdout.WriteLine($"desired: {detection.DesiredDisplay}");
                stdout.WriteLine($"matches: {detection.MatchesDesired}");
                stdout.WriteLine("simulate: no writes");
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "recommend"))
        {
            var profile = ParseProfile(GetOption(args, "--profile"));
            var inventory = services.Audit();
            var catalog = services.LoadCatalog();
            var detections = TweakDetector.DetectAll(catalog, services.Registry());
            var recs = RecommendationEngine.Rank(catalog, detections, profile, inventory);
            if (json)
            {
                WriteJson(stdout, "recommend", ExitCodes.Ok, new { profile, recs });
            }
            else
            {
                stdout.WriteLine($"profile: {profile}");
                foreach (var rec in recs)
                {
                    stdout.WriteLine($"{rec.Score:0.000}\t{rec.TweakId}\t{rec.Why}");
                }
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "app") && EqualsCmd(args, 1, "list"))
        {
            var apps = services.LoadApplications();
            var category = GetOption(args, "--category");
            if (!string.IsNullOrWhiteSpace(category))
            {
                apps = apps.Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            var facts = InstalledAppDetector.Scan();
            if (json)
            {
                WriteJson(stdout, "app list", ExitCodes.Ok, apps.Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Category,
                    a.WingetId,
                    a.Evidence,
                    a.Risk,
                    installed = InstalledAppDetector.IsInstalled(a, facts),
                    a.Axes
                }));
            }
            else
            {
                foreach (var a in apps)
                {
                    var installed = InstalledAppDetector.IsInstalled(a, facts) ? "installed" : "missing";
                    stdout.WriteLine($"{a.Id}\t{a.Category}\t{a.Evidence}\t{installed}\t{a.WingetId}\t{a.Title}");
                }
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "app") && EqualsCmd(args, 1, "recommend"))
        {
            var profile = ParseProfile(GetOption(args, "--profile"));
            var category = GetOption(args, "--category");
            var apps = services.LoadApplications();
            var facts = InstalledAppDetector.Scan();
            var installedIds = apps
                .Where(a => InstalledAppDetector.IsInstalled(a, facts))
                .Select(a => a.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var recs = AppRecommendationEngine.Rank(apps, profile, installedIds, category);
            if (json)
            {
                WriteJson(stdout, "app recommend", ExitCodes.Ok, new { profile, category, weights = AppRecommendationEngine.WeightsFor(profile), recs });
            }
            else
            {
                stdout.WriteLine($"profile: {profile}");
                foreach (var rec in recs)
                {
                    stdout.WriteLine($"{rec.Score:0.000}\t{rec.AppId}\t{(rec.Installed ? "installed" : "missing")}\t{rec.Why}");
                }
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "app") && EqualsCmd(args, 1, "install"))
        {
            if (!HasFlag(args, "--yes"))
            {
                stderr.WriteLine("app install requires --yes (explicit approval).");
                return ExitCodes.Generic;
            }

            if (args.Count < 3 || args[2].StartsWith('-'))
            {
                stderr.WriteLine("Usage: windowslab-cli app install <id> --yes [--output json]");
                return ExitCodes.Generic;
            }

            var id = args[2];
            var apps = services.LoadApplications();
            var app = apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
            if (app is null)
            {
                stderr.WriteLine($"App not found: {id}");
                return 10;
            }

            var result = WingetClient.Install(app, allowElevate: true);
            if (json)
            {
                WriteJson(stdout, "app install",
                    result.Outcome is WingetOutcome.Ok or WingetOutcome.AlreadyInstalled ? ExitCodes.Ok : ExitCodes.Generic,
                    result);
            }
            else
            {
                stdout.WriteLine($"outcome: {result.Outcome}");
                stdout.WriteLine($"message: {result.Message}");
                if (result.LogPath is not null)
                {
                    stdout.WriteLine($"log: {result.LogPath}");
                }
            }

            return result.Outcome is WingetOutcome.Ok or WingetOutcome.AlreadyInstalled
                ? ExitCodes.Ok
                : result.Outcome == WingetOutcome.Denied
                    ? ExitCodes.PolicyBlocked
                    : ExitCodes.Generic;
        }

        if (EqualsCmd(args, 0, "preset") && EqualsCmd(args, 1, "apply"))
        {
            if (!HasFlag(args, "--yes"))
            {
                stderr.WriteLine("preset apply requires --yes [--i-am-on-lab-vm] [--dry-run]");
                return ExitCodes.Generic;
            }

            if (args.Count < 3 || args[2].StartsWith('-'))
            {
                stderr.WriteLine("Usage: windowslab-cli preset apply <id> --yes [--i-am-on-lab-vm] [--dry-run]");
                return ExitCodes.Generic;
            }

            var presetId = args[2];
            var presets = services.LoadPresets();
            var preset = presets.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));
            if (preset is null)
            {
                stderr.WriteLine($"Preset not found: {presetId}");
                return 10;
            }

            var catalog = services.LoadCatalog();
            var inventory = services.Audit();
            var detections = TweakDetector.DetectAll(catalog, services.Registry());
            var checks = ChecklistEvaluator.Evaluate(services.LoadChecklists(), inventory, services.Registry(), services.WindowsIdentityName());
            var eval = PresetEvaluator.Evaluate(preset, catalog, detections, checks);
            var ids = eval.Items
                .Where(i => i.Estado == "FALTA" && i.Kind != "step")
                .Select(i => i.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var tweaks = ids
                .Select(id => catalog.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Where(t => t is not null)
                .Cast<TweakDefinition>()
                .Where(t => TweakApplicator.IsApplyEligible(t) || TweakApplicator.IsLabEligible(t))
                .ToArray();

            var securityTweaks = tweaks.Where(t => t.AffectsSecurity).ToArray();
            if (securityTweaks.Length > 0 && !HasFlag(args, "--i-accept-security-impact"))
            {
                stderr.WriteLine(
                    $"Policy (D021): preset contains {securityTweaks.Length} security-affecting tweak(s). " +
                    "Add --i-accept-security-impact to proceed.");
                return ExitCodes.PolicyBlocked;
            }

            if (tweaks.Length == 0)
            {
                stderr.WriteLine("No eligible pending tweaks in preset.");
                return ExitCodes.Ok;
            }

            var catalogDir = CatalogLocator.FindTweaksDirectory()
                             ?? throw new DirectoryNotFoundException("catalog/tweaks not found");
            var job = SystemApplyEngine.ApplyAsync(
                tweaks,
                new FileBackupStore(),
                services.Registry(),
                OperatorSettingsStore.Load(),
                HasFlag(args, "--i-am-on-lab-vm"),
                HasFlag(args, "--dry-run"),
                catalogDir).GetAwaiter().GetResult();

            if (json)
            {
                WriteJson(stdout, "preset apply", job.Succeeded ? ExitCodes.Ok : ExitCodes.Generic, job);
            }
            else
            {
                stdout.WriteLine($"succeeded: {job.Succeeded}");
                stdout.WriteLine($"message: {job.Message}");
                if (job.BackupId is not null)
                {
                    stdout.WriteLine($"backupId: {job.BackupId}");
                }
            }

            return job.Succeeded ? ExitCodes.Ok : ExitCodes.Generic;
        }

        if (EqualsCmd(args, 0, "preset") && (EqualsCmd(args, 1, "list") || args.Count == 1))
        {
            var presets = services.LoadPresets();
            var catalog = services.LoadCatalog();
            var inventory = services.Audit();
            var detections = TweakDetector.DetectAll(catalog, services.Registry());
            var checks = ChecklistEvaluator.Evaluate(services.LoadChecklists(), inventory, services.Registry(), services.WindowsIdentityName());
            var evals = PresetEvaluator.EvaluateAll(presets, catalog, detections, checks);
            if (json)
            {
                WriteJson(stdout, "preset list", ExitCodes.Ok, evals.Select(e => new
                {
                    e.Preset.Id,
                    e.Preset.Title,
                    e.ReadyCount,
                    e.GapCount,
                    e.Total,
                    e.Preset.IsCustom
                }));
            }
            else
            {
                foreach (var e in evals)
                {
                    stdout.WriteLine($"{e.Preset.Id}\t{e.ReadyCount}/{e.Total}\t{e.Preset.Title}");
                }
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "preset") && (EqualsCmd(args, 1, "show") || EqualsCmd(args, 1, "simulate")))
        {
            if (args.Count < 3)
            {
                stderr.WriteLine("Usage: windowslab-cli preset show <id>");
                return ExitCodes.Generic;
            }

            var id = args[2];
            var presets = services.LoadPresets();
            var preset = presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (preset is null)
            {
                stderr.WriteLine($"Preset not found: {id}");
                return 10;
            }

            var catalog = services.LoadCatalog();
            var inventory = services.Audit();
            var detections = TweakDetector.DetectAll(catalog, services.Registry());
            var checks = ChecklistEvaluator.Evaluate(services.LoadChecklists(), inventory, services.Registry(), services.WindowsIdentityName());
            var eval = PresetEvaluator.Evaluate(preset, catalog, detections, checks);
            if (json)
            {
                WriteJson(stdout, "preset show", ExitCodes.Ok, new { eval.Preset.Id, eval.ReadyCount, eval.GapCount, eval.Total, simulateOnly = true, wouldWrite = false, eval.Items });
            }
            else
            {
                stdout.WriteLine($"preset: {eval.Preset.Id}");
                stdout.WriteLine($"title: {eval.Preset.Title}");
                stdout.WriteLine($"ready: {eval.ReadyCount}/{eval.Total}");
                stdout.WriteLine("simulate: no writes");
                foreach (var item in eval.Items)
                {
                    stdout.WriteLine($"{item.Estado}\t{item.Title}\tactual={item.Actual}\tdesired={item.Desired}");
                }
            }

            return ExitCodes.Ok;
        }

        if (EqualsCmd(args, 0, "checklist"))
        {
            var items = services.LoadChecklists();
            var inventory = services.Audit();
            var results = ChecklistEvaluator.Evaluate(items, inventory, services.Registry(), services.WindowsIdentityName());
            if (json)
            {
                WriteJson(stdout, "checklist", ExitCodes.Ok, new
                {
                    summary = new
                    {
                        ok = results.Count(r => r.Verdict == ChecklistVerdict.Ok),
                        gap = results.Count(r => r.Verdict == ChecklistVerdict.Gap),
                        info = results.Count(r => r.Verdict == ChecklistVerdict.Info),
                        unknown = results.Count(r => r.Verdict == ChecklistVerdict.Unknown),
                        total = results.Count
                    },
                    limits = "Windows 11 Home/Pro cannot fully disable telemetry (Required=1). Do not kill Defender/DiagTrack/SysMain/Search.",
                    results
                });
            }
            else
            {
                stdout.WriteLine("checklist: baseline");
                stdout.WriteLine("limits: no telemetry-zero on Pro/Home; do not kill system processes");
                string? section = null;
                foreach (var r in results)
                {
                    if (section != r.Section)
                    {
                        section = r.Section;
                        stdout.WriteLine();
                        stdout.WriteLine($"[{section}]");
                    }

                    stdout.WriteLine($"{VerdictLabel(r.Verdict)}\t{r.Title}\tactual={r.Actual}\tdesired={r.Desired}\t{r.Note}");
                }

                stdout.WriteLine();
                stdout.WriteLine($"ok={results.Count(r => r.Verdict == ChecklistVerdict.Ok)} gap={results.Count(r => r.Verdict == ChecklistVerdict.Gap)} info={results.Count(r => r.Verdict == ChecklistVerdict.Info)} unknown={results.Count(r => r.Verdict == ChecklistVerdict.Unknown)}");
            }

            return ExitCodes.Ok;
        }

        stderr.WriteLine("Unknown command. Use windowslab-cli --help.");
        return ExitCodes.Generic;
    }

    private static void WriteLive(TextWriter stdout, LiveDashboardSnapshot dash)
    {
        stdout.WriteLine($"captured: {dash.CapturedUtc:u}");
        stdout.WriteLine($"host: {dash.ComputerName}");
        stdout.WriteLine($"user: {dash.UserName}");
        stdout.WriteLine($"os: {dash.OsLine}");
        stdout.WriteLine($"cpuPercent: {dash.CpuPercent:0.0}");
        stdout.WriteLine(
            $"ram: used={dash.Ram.UsedGb:0.0}GB avail={dash.Ram.AvailableGb:0.0}GB total={dash.Ram.TotalGb:0.0}GB ({dash.Ram.Percent:0.0}%)");
        if (dash.Ram.CommitPercent is not null)
        {
            stdout.WriteLine(
                $"commit: {dash.Ram.CommitUsedGb:0.0}/{dash.Ram.CommitLimitGb:0.0} GB ({dash.Ram.CommitPercent:0.0}%)");
        }

        foreach (var d in dash.Disks)
        {
            stdout.WriteLine($"disk: {d.Root} free={d.FreeGb:0.0}GB total={d.TotalGb:0.0}GB ({d.FreePercent:0.0}% free)");
        }

        if (dash.DiskIo.PercentDiskTime is not null)
        {
            stdout.WriteLine(
                $"diskIo: percentTime={dash.DiskIo.PercentDiskTime:0.0} queue={dash.DiskIo.AvgQueueLength:0.00}");
        }

        stdout.WriteLine($"network: {dash.NetworkLine}");
        if (dash.Processes is null)
        {
            return;
        }

        foreach (var g in dash.Processes.GroupTotals)
        {
            stdout.WriteLine($"group: {g.Group} count={g.Count} workingSetBytes={g.WorkingSetBytes}");
        }

        stdout.WriteLine("topByWorkingSet:");
        foreach (var p in dash.Processes.TopByWorkingSet.Take(15))
        {
            stdout.WriteLine(
                $"  {p.Group}\t{p.Name}\tpid={p.Id}\tcpu={p.CpuPercent:0.0}%\tws={p.WorkingSetBytes}\tpriv={p.PrivateBytes}");
        }
    }

    private static string VerdictLabel(ChecklistVerdict verdict) => verdict switch
    {
        ChecklistVerdict.Ok => "OK",
        ChecklistVerdict.Gap => "FALTA",
        ChecklistVerdict.Unknown => "?",
        _ => "INFO"
    };

    private static UserProfile ParseProfile(string? value) => value?.ToLowerInvariant() switch
    {
        "developer" => UserProfile.Developer,
        "gaming" => UserProfile.Gaming,
        "virtualization" => UserProfile.Virtualization,
        _ => UserProfile.Balanced
    };

    private static bool HasFlag(IReadOnlyList<string> args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool EqualsCmd(IReadOnlyList<string> args, int index, string value) =>
        args.Count > index && string.Equals(args[index], value, StringComparison.OrdinalIgnoreCase);

    private static bool IsHelp(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (arg is "-h" or "--help" or "-?" or "help")
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteOs(TextWriter stdout, OsIdentity identity)
    {
        stdout.WriteLine($"family: {identity.FamilyLabel}");
        stdout.WriteLine($"isWindows11: {identity.IsWindows11}");
        stdout.WriteLine($"build: {identity.Build}");
        stdout.WriteLine($"ubr: {identity.Ubr}");
        stdout.WriteLine($"displayVersion: {identity.DisplayVersion}");
        stdout.WriteLine($"editionId: {identity.EditionId}");
        stdout.WriteLine($"productName: {identity.ProductName} (not used for family)");
        if (identity.CompositionEditionId is not null)
        {
            stdout.WriteLine($"compositionEditionId: {identity.CompositionEditionId}");
        }

        if (identity.ProductNameWarning is not null)
        {
            stdout.WriteLine($"warning: {identity.ProductNameWarning}");
        }
    }

    private static void WriteInventory(TextWriter stdout, MachineInventory inv)
    {
        stdout.WriteLine($"os: {inv.Os.FamilyLabel} {inv.Os.DisplayVersion} build {inv.Os.Build}");
        stdout.WriteLine($"cpu: {inv.Cpu.Name} ({inv.Cpu.LogicalProcessors} threads)");
        stdout.WriteLine($"ram: {inv.Ram.TotalBytes / (1024.0 * 1024 * 1024):0.0} GB");
        foreach (var gpu in inv.Gpus)
        {
            var vram = gpu.DedicatedBytes is null ? "vram unknown" : $"{gpu.DedicatedBytes.Value / (1024.0 * 1024 * 1024):0.0} GB ({gpu.VramSource})";
            stdout.WriteLine($"gpu: {gpu.Name} [{gpu.Vendor}] {vram}");
        }

        foreach (var vol in inv.Volumes)
        {
            stdout.WriteLine($"vol: {vol.Root} free {vol.FreeBytes / (1024.0 * 1024 * 1024):0.0} GB / {vol.TotalBytes / (1024.0 * 1024 * 1024):0.0} GB");
        }

        stdout.WriteLine($"board: {inv.Board.Manufacturer} {inv.Board.Product} BIOS {inv.Board.BiosVersion}");
        stdout.WriteLine($"security: VBS={inv.Security.VirtualizationBasedSecurityStatus} defender={inv.Security.DefenderEnabled} firewall={inv.Security.FirewallEnabled}");
        stdout.WriteLine($"tools: {string.Join(", ", inv.Toolchain.Present)}");
        stdout.WriteLine($"probes: {inv.Probes.Count}");
    }

    private static void WriteJson(TextWriter stdout, string command, int exit, object data)
    {
        var envelope = new
        {
            schemaVersion = SchemaVersion,
            command,
            exitCode = exit,
            timestamp = DateTimeOffset.UtcNow,
            data
        };
        stdout.WriteLine(JsonSerializer.Serialize(envelope, JsonDefaults.Options));
    }
}

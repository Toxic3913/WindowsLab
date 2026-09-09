using System.Text.Json;
using WindowsLab.Audit;
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
}

public static class CliApp
{
    public const int SchemaVersion = 1;

    public const string HelpText =
        """
        WindowsLab — Beta 0 (solo lectura)

        Uso:
          windowslab --help
          windowslab audit
          windowslab audit --os
          windowslab audit --output json
          windowslab tweak list
          windowslab tweak detect <id>
          windowslab tweak simulate <id>
          windowslab recommend [--profile balanced|developer|gaming|virtualization]
          windowslab recommend --output json
          windowslab checklist
          windowslab checklist --output json
          windowslab preset list
          windowslab preset show <id>
          windowslab preset simulate <id>

        tweak apply / preset apply  Bloqueado en Beta 0 (exit 13). No escribe el registro.

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

        if (EqualsCmd(args, 0, "tweak") && EqualsCmd(args, 1, "apply"))
        {
            stderr.WriteLine("Beta 0: apply is disabled (policy). Use tweak detect / simulate.");
            return ExitCodes.PolicyBlocked;
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
                stderr.WriteLine("Usage: windowslab tweak detect <id>");
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
                stdout.WriteLine("simulate: no writes (Beta 0)");
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

        if (EqualsCmd(args, 0, "preset") && EqualsCmd(args, 1, "apply"))
        {
            stderr.WriteLine("Beta 0: preset apply is disabled (policy). Use preset simulate / the Configurar page.");
            return ExitCodes.PolicyBlocked;
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
                stderr.WriteLine("Usage: windowslab preset show <id>");
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
                stdout.WriteLine("simulate: no writes (Beta 0)");
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
                    limits = "Windows 11 Home/Pro cannot fully disable telemetry (Required=1). Do not kill Defender/DiagTrack/SysMain/Search. Beta 0 does not apply.",
                    results
                });
            }
            else
            {
                stdout.WriteLine("checklist: baseline (read-only)");
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

        stderr.WriteLine("Unknown command. Use windowslab --help.");
        return ExitCodes.Generic;
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

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsLab.Applications;
using WindowsLab.Backup;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public partial class MainWindow
{
    private void RefreshLivePreview()
    {
        try
        {
            var s = LiveSystemReader.Read();
            LivePreview.Text =
                Loc.T("resources.liveHint") + "\n" +
                $"{s.OsLine}\nCPU {s.CpuLine}\nRAM {s.RamLine}\n{s.DiskLine}\nIP {s.NetworkLine}\n{s.CapturedUtc:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            LivePreview.Text = ex.Message;
        }
    }

    private void RenderSystem()
    {
        if (_session is null)
        {
            return;
        }

        SystemPanel.Children.Clear();
        var inv = _session.Inventory;
        AddLine(SystemPanel, $"OS: {inv.Os.FamilyLabel} {inv.Os.DisplayVersion} {inv.Os.EditionId}  ProductName={inv.Os.ProductName} (ignorado)");
        AddLine(SystemPanel, $"CPU: {inv.Cpu.Name}  cores={inv.Cpu.Cores}  threads={inv.Cpu.LogicalProcessors}  {inv.Cpu.MaxClockMhz} MHz  [{inv.Cpu.Status}]");
        AddLine(SystemPanel, $"RAM: {inv.Ram.TotalBytes / (1024d * 1024 * 1024):0.0} GB  módulos={inv.Ram.ModuleCount}  {inv.Ram.SpeedMhz} MT/s  [{inv.Ram.Status}]");
        foreach (var gpu in inv.Gpus)
        {
            var gb = gpu.DedicatedBytes is null ? "?" : $"{gpu.DedicatedBytes.Value / (1024d * 1024 * 1024):0.1} GB";
            AddLine(SystemPanel, $"GPU: {gpu.Name}  {gpu.Vendor}  VRAM {gb} via {gpu.VramSource}");
        }

        AddLine(SystemPanel, $"Placa: {inv.Board.Manufacturer} {inv.Board.Product}  BIOS {inv.Board.BiosVersion} {inv.Board.BiosDate}");
        foreach (var vol in inv.Volumes)
        {
            AddLine(SystemPanel, $"Disco {vol.Root} {vol.Label}  {vol.FreeBytes / (1024d * 1024 * 1024):0.0} / {vol.TotalBytes / (1024d * 1024 * 1024):0.0} GB  {vol.DriveType}");
        }

        AddLine(SystemPanel, "Herramientas: " + string.Join(", ", inv.Toolchain.Present));
    }

    private void RenderSecurity()
    {
        if (_session is null)
        {
            return;
        }

        SecurityPanel.Children.Clear();
        var s = _session.Inventory.Security;
        AddLine(SecurityPanel, $"VBS (Device Guard): {s.VirtualizationBasedSecurityStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "denied/unknown"}");
        AddLine(SecurityPanel, $"Defender AntivirusEnabled: {Fmt(s.DefenderEnabled)}  RTP: {Fmt(s.RealTimeProtection)}");
        AddLine(SecurityPanel, $"Firewall (perfil estándar): {Fmt(s.FirewallEnabled)}");
        AddLine(SecurityPanel, s.Notes ?? "Sin notas.");
        AddLine(SecurityPanel, Loc.IsEnglish
            ? "WindowsLab never recommends turning off Defender, HVCI, or Secure Boot by default."
            : "WindowsLab no recomienda desactivar Defender, HVCI ni Secure Boot por defecto.");
    }

    private void RenderTweaks()
    {
        if (_session is null)
        {
            return;
        }

        var rows = _session.Catalog.Select(t =>
        {
            var d = _session.Detections.FirstOrDefault(x => x.TweakId == t.Id);
            return new TweakRow
            {
                Id = t.Id,
                Title = t.Title,
                Category = t.Category,
                Risk = t.Risk.ToString(),
                Evidence = t.Evidence.ToString(),
                Actual = d?.ActualDisplay,
                Desired = d?.DesiredDisplay,
                Match = d?.MatchesDesired,
                Status = d?.Status.ToString()
            };
        }).ToList();
        TweakGrid.ItemsSource = rows;
    }

    private void RenderAdvice()
    {
        if (_session is null)
        {
            return;
        }

        AdviceTipsList.ItemsSource = BuildAdviceTips();
        AdviceGrid.ItemsSource = _session.Recommendations.Select(r => new
        {
            r.Score,
            r.TweakId,
            r.Title,
            r.Category,
            r.Risk,
            r.Evidence,
            r.Why
        }).ToList();
    }

    private static List<AdviceTip> BuildAdviceTips() =>
    [
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Microsoft Edge background" : "Microsoft Edge en segundo plano",
            Body = Loc.IsEnglish
                ? "Turn off Startup boost and background apps. Do not force-kill msedge.exe or remove Edge with random scripts (breaks WebView2)."
                : "Desactiva inicio al arrancar y apps en segundo plano. No mates msedge.exe ni desinstales Edge con scripts (rompe WebView2).",
            Uri = "ms-settings:startupapps"
        },
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Startup apps" : "Apps al inicio",
            Body = Loc.IsEnglish
                ? "Disable OneDrive, Teams, Outlook, Phone Link, Xbox Game Bar if unused. Prefer Settings over Task Manager End task loops."
                : "Quita OneDrive, Teams, Outlook, Phone Link, Xbox Game Bar si no los usas. Mejor Configuración que matar procesos en bucle.",
            Uri = "ms-settings:startupapps"
        },
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Widgets / taskbar feed" : "Widgets / feed de barra",
            Body = Loc.IsEnglish
                ? "Widgets keep network/CPU awake. Turn them off under Taskbar settings."
                : "Los widgets mantienen red/CPU. Apágalos en Personalización > Barra de tareas.",
            Uri = "ms-settings:personalization-taskbar"
        },
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Do not kill" : "No matar",
            Body = Loc.IsEnglish
                ? "WindowsLab never recommends killing Defender (MsMpEng), SearchHost, SysMain, or DiagTrack as a default 'optimization'."
                : "WindowsLab no recomienda matar Defender (MsMpEng), SearchHost, SysMain ni DiagTrack como 'optimización' por defecto.",
            Uri = "ms-settings:windowsdefender"
        }
    ];

    private void AdviceTipOpen_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AdviceTip tip)
        {
            Launch(tip.Uri, tip.Body);
        }
    }

    private void RenderChecklist()
    {
        if (_session is null)
        {
            return;
        }

        var rows = _session.ChecklistResults.Select(r => new ChecklistRow
        {
            Id = r.Id,
            Estado = r.Verdict switch
            {
                ChecklistVerdict.Ok => "OK",
                ChecklistVerdict.Gap => "FALTA",
                ChecklistVerdict.Unknown => "?",
                _ => "INFO"
            },
            Section = r.Section,
            Title = r.Title,
            HowTo = r.HowTo,
            SettingsUri = r.SettingsUri,
            Verdict = r.Verdict,
            Policy = r.Policy
        }).ToList();
        ChecklistGrid.ItemsSource = rows;
        var ok = rows.Count(x => x.Estado == "OK");
        var gap = rows.Count(x => x.Estado == "FALTA");
        var info = rows.Count(x => x.Estado == "INFO");
        var unk = rows.Count(x => x.Estado == "?");
        ChecklistSummary.Text = $"OK {ok}  ·  FALTA {gap}  ·  INFO {info}  ·  ? {unk}  ·  total {rows.Count}";
    }

    private void ChecklistActivate_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ChecklistRow row)
        {
            return;
        }

        if (row.Policy == ChecklistPolicy.NeverDisable)
        {
            MessageBox.Show(row.HowTo, row.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Prefer linked HKCU lab tweak when title/id maps to catalog and is eligible.
        if (_session is not null)
        {
            var tweak = MapChecklistToTweak(row.Id, _session.Catalog);
            if (tweak is not null && TweakApplicator.IsLabEligible(tweak))
            {
                var confirm = MessageBox.Show(
                    Loc.IsEnglish
                        ? $"Apply HKCU tweak {tweak.Id}?\n{tweak.Title}"
                        : $"¿Aplicar tweak HKCU {tweak.Id}?\n{tweak.Title}",
                    "WindowsLab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    var result = TweakApplicator.Apply(tweak, new LiveRegistryReader(), new LiveRegistryWriter(), dryRun: false);
                    if (result.Backup is not null)
                    {
                        TweakApplicator.PersistBackup(result.Backup);
                    }

                    MessageBox.Show(result.Message, "Apply", MessageBoxButton.OK,
                        result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Warning);
                    Reload(ParseProfile((ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString()));
                    return;
                }
            }
        }

        var uri = row.SettingsUri
                  ?? GuessSettingsUri(row.Id, row.Section);
        if (!string.IsNullOrWhiteSpace(uri))
        {
            Launch(uri, row.HowTo);
            ChecklistSummary.Text = (Loc.IsEnglish ? "Opened: " : "Abierto: ") + uri + " — " + row.Title;
            return;
        }

        MessageBox.Show(row.HowTo, row.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static TweakDefinition? MapChecklistToTweak(string checklistId, IReadOnlyList<TweakDefinition> catalog)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["telemetry.tailored-off"] = "privacy.tailored-experiences-off",
            ["telemetry.advertising-id-off"] = "privacy.advertising-id-off",
            ["telemetry.ink-off"] = "privacy.ink-personalization-off",
            ["telemetry.text-off"] = "privacy.restrict-implicit-text",
            ["telemetry.content-delivery-off"] = "privacy.content-delivery-off"
        };
        if (!map.TryGetValue(checklistId, out var tweakId))
        {
            return null;
        }

        return catalog.FirstOrDefault(t => string.Equals(t.Id, tweakId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GuessSettingsUri(string id, string section)
    {
        if (id.StartsWith("bg.", StringComparison.OrdinalIgnoreCase)
            || section.Contains("segundo plano", StringComparison.OrdinalIgnoreCase)
            || section.Contains("background", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:startupapps";
        }

        if (id.StartsWith("telemetry.", StringComparison.OrdinalIgnoreCase)
            || section.Contains("Telemetr", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:privacy-feedback";
        }

        if (id.StartsWith("account.", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:yourinfo";
        }

        if (section.Contains("Programas", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:appsfeatures";
        }

        return null;
    }

    private void Simulate_OnClick(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            SimulateText.Text = "Aún cargando.";
            return;
        }

        if (TweakGrid.SelectedItem is not TweakRow row)
        {
            SimulateText.Text = "Selecciona un tweak.";
            return;
        }

        var detection = _session.Detections.FirstOrDefault(d => d.TweakId == row.Id);
        SimulateText.Text = detection is null
            ? "Sin detección."
            : $"Simulación (0 escrituras): {detection.TweakId} actual={detection.ActualDisplay} desired={detection.DesiredDisplay} match={detection.MatchesDesired}";
    }

    private static void AddLine(Panel panel, string text)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private static string Fmt(bool? value) => value is null ? "unknown" : value.Value ? "on" : "off";
}

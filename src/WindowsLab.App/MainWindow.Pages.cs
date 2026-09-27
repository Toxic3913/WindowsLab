using System.Windows;
using System.Windows.Controls;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public partial class MainWindow
{
    private void RenderPending()
    {
        if (_session is null)
        {
            return;
        }

        var checklist = _session.ChecklistResults
            .Where(r => r.Verdict is ChecklistVerdict.Gap or ChecklistVerdict.Unknown)
            .Select(r => new ChecklistRow
            {
                Id = r.Id,
                Estado = r.Verdict switch
                {
                    ChecklistVerdict.Gap => Loc.T("home.pending.gap"),
                    ChecklistVerdict.Unknown => "?",
                    _ => "INFO"
                },
                Section = r.Section,
                Title = r.Title,
                HowTo = r.HowTo,
                SettingsUri = r.SettingsUri,
                Verdict = r.Verdict,
                Policy = r.Policy
            });

        var recommended = _session.Recommendations
            .Where(r =>
            {
                var d = _session.Detections.FirstOrDefault(x => x.TweakId == r.TweakId);
                return d?.MatchesDesired != true;
            })
            .Take(12)
            .Select(r => new ChecklistRow
            {
                Id = r.TweakId,
                Estado = Loc.IsEnglish ? "REC" : "REC",
                Section = r.Category,
                Title = r.Title,
                HowTo = r.Why,
                SettingsUri = null,
                Verdict = ChecklistVerdict.Gap,
                Policy = ChecklistPolicy.Recommend
            });

        PendingGrid.ItemsSource = checklist.Concat(recommended).Take(40).ToList();
    }

    private void PendingActivate_OnClick(object sender, RoutedEventArgs e)
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

        if (_session is not null && row.Estado == "REC")
        {
            _selectedTweakIds.Add(row.Id);
            foreach (var t in _allTweakRows.Where(x => string.Equals(x.Id, row.Id, StringComparison.OrdinalIgnoreCase)))
            {
                t.IsSelected = true;
            }

            SelectNav("adjust");
            RefreshTweakCategoryView();
            AdjustStatus.Text = Loc.IsEnglish
                ? $"Checked recommendation: {row.Title}"
                : $"Marcado recomendado: {row.Title}";
            return;
        }

        if (_session is not null)
        {
            var tweak = MapChecklistToTweak(row.Id, _session.Catalog);
            if (tweak is not null)
            {
                if (_lastMontageId is not null)
                {
                    SelectMontage(_lastMontageId);
                }

                _selectedTweakIds.Add(tweak.Id);
                foreach (var t in _allTweakRows.Where(x => string.Equals(x.Id, tweak.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    t.IsSelected = true;
                }

                SelectNav("adjust");
                RefreshTweakCategoryView();
                return;
            }
        }

        var uri = row.SettingsUri ?? GuessSettingsUri(row.Id, row.Section);
        if (!string.IsNullOrWhiteSpace(uri))
        {
            Launch(uri, row.HowTo);
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

    private void RenderSystem()
    {
        if (_session is null)
        {
            return;
        }

        SystemPanel.Children.Clear();
        var inv = _session.Inventory;
        AddLine(SystemPanel, $"OS: {inv.Os.FamilyLabel} {inv.Os.DisplayVersion} {inv.Os.EditionId}");
        AddLine(SystemPanel, $"CPU: {inv.Cpu.Name}  cores={inv.Cpu.Cores}  threads={inv.Cpu.LogicalProcessors}  {inv.Cpu.MaxClockMhz} MHz");
        AddLine(SystemPanel, $"RAM: {inv.Ram.TotalBytes / (1024d * 1024 * 1024):0.0} GB  modules={inv.Ram.ModuleCount}  {inv.Ram.SpeedMhz} MT/s");
        foreach (var gpu in inv.Gpus)
        {
            var gb = gpu.DedicatedBytes is null ? "?" : $"{gpu.DedicatedBytes.Value / (1024d * 1024 * 1024):0.1} GB";
            AddLine(SystemPanel, $"GPU: {gpu.Name}  {gpu.Vendor}  VRAM {gb}");
        }

        AddLine(SystemPanel, $"Board: {inv.Board.Manufacturer} {inv.Board.Product}  BIOS {inv.Board.BiosVersion}");
        foreach (var vol in inv.Volumes)
        {
            AddLine(SystemPanel, $"Disk {vol.Root} {vol.Label}  {vol.FreeBytes / (1024d * 1024 * 1024):0.0} / {vol.TotalBytes / (1024d * 1024 * 1024):0.0} GB");
        }
    }

    private void RenderSecurity()
    {
        if (_session is null)
        {
            return;
        }

        SecurityPanel.Children.Clear();
        var s = _session.Inventory.Security;
        AddLine(SecurityPanel, $"VBS: {s.VirtualizationBasedSecurityStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}");
        AddLine(SecurityPanel, $"Defender enabled: {Fmt(s.DefenderEnabled)}  RTP: {Fmt(s.RealTimeProtection)}");
        AddLine(SecurityPanel, $"Firewall: {Fmt(s.FirewallEnabled)}");
        AddLine(SecurityPanel, s.Notes ?? "");
        AddLine(SecurityPanel, Loc.IsEnglish
            ? "WindowsLab never recommends turning off Defender, HVCI, or Secure Boot by default."
            : "WindowsLab no recomienda desactivar Defender, HVCI ni Secure Boot por defecto.");
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

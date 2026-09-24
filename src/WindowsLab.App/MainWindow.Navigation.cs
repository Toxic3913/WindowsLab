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
    private void Nav_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageHome is null || PageConfig is null || PageResources is null || PagePerformance is null)
        {
            return;
        }

        if (Nav.SelectedItem is not ListBoxItem item || item.Tag is not string tag || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        ShowPage(tag);
        if (tag == "resources")
        {
            RefreshLivePreview();
        }

        if (tag == "performance")
        {
            RefreshPerformanceDashboard();
            _perfTimer.Start();
        }
        else
        {
            _perfTimer.Stop();
        }
    }

    private void ShowPage(string tag)
    {
        PageHome.Visibility = tag == "home" ? Visibility.Visible : Visibility.Collapsed;
        PageConfig.Visibility = tag == "config" ? Visibility.Visible : Visibility.Collapsed;
        PageResources.Visibility = tag == "resources" ? Visibility.Visible : Visibility.Collapsed;
        PagePerformance.Visibility = tag == "performance" ? Visibility.Visible : Visibility.Collapsed;
        PageSystem.Visibility = tag == "system" ? Visibility.Visible : Visibility.Collapsed;
        PageSecurity.Visibility = tag == "security" ? Visibility.Visible : Visibility.Collapsed;
        PageTweaks.Visibility = tag == "tweaks" ? Visibility.Visible : Visibility.Collapsed;
        PageAdvice.Visibility = tag == "advice" ? Visibility.Visible : Visibility.Collapsed;
        PageChecklist.Visibility = tag == "checklist" ? Visibility.Visible : Visibility.Collapsed;
        PageApps.Visibility = tag == "apps" ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectNav(string tag)
    {
        foreach (var obj in Nav.Items)
        {
            if (obj is ListBoxItem item && string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                Nav.SelectedItem = item;
                return;
            }
        }
    }

    private void RenderHome()
    {
        if (_session is null)
        {
            return;
        }

        var inv = _session.Inventory;
        HomeSummary.Text =
            $"{inv.Os.FamilyLabel} {inv.Os.DisplayVersion}  ·  {inv.Cpu.Name}  ·  {inv.Ram.TotalBytes / (1024d * 1024 * 1024):0.0} GB";

        var c = _session.SystemVolume;
        if (c is not null)
        {
            var freeGb = c.FreeBytes / (1024d * 1024 * 1024);
            HomeWarn.Text = freeGb < 20
                ? $"C: {freeGb:0.0} GB libres — espacio bajo."
                : "";
        }
        else
        {
            HomeWarn.Text = "";
        }

        UpdateModeButton(BtnModeGamingStat, "gaming");
        UpdateModeButton(BtnModeOptimizedStat, "perf.max");
        UpdateModeButton(BtnModeDevStat, "developer");
        UpdateModeButton(BtnModeBalancedStat, "privacy.lab");
        UpdateModeButton(BtnModeEmpresaStat, "stack.empresa");
        UpdateModeButton(BtnModePruebasStat, "stack.pruebas");

        HomePresetList.ItemsSource = _session.PresetEvals
            .Where(p => !p.Preset.IsCustom)
            .Select(e => new PresetPick { Eval = e })
            .ToList();
        ProbeList.ItemsSource = inv.Probes.Select(p => $"{p.Status,-12} {p.ProbeId}  {p.Message}").ToArray();
    }

    private void UpdateModeButton(TextBlock stat, string presetId)
    {
        if (_session is null)
        {
            stat.Text = "";
            return;
        }

        var eval = _session.PresetEvals.FirstOrDefault(p =>
            string.Equals(p.Preset.Id, presetId, StringComparison.OrdinalIgnoreCase));
        if (eval is null || eval.Total == 0)
        {
            stat.Text = Loc.IsEnglish ? "Pack missing" : "Pack no encontrado";
            return;
        }

        stat.Text = Loc.IsEnglish
            ? $"{eval.ReadyCount}/{eval.Total} ready · {eval.GapCount} gaps"
            : $"{eval.ReadyCount}/{eval.Total} listos · {eval.GapCount} pendientes";
    }

    private void QuickMode_OnClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || sender is not Button btn || btn.Tag is not string mode)
        {
            return;
        }

        var (profile, presetId, labelKey) = mode.ToLowerInvariant() switch
        {
            "gaming" => (UserProfile.Gaming, "gaming", "mode.gaming"),
            "optimized" => (UserProfile.Balanced, "perf.max", "mode.optimized"),
            "developer" => (UserProfile.Developer, "developer", "mode.dev"),
            "balanced" => (UserProfile.Balanced, "privacy.lab", "mode.balanced"),
            "empresa" => (UserProfile.Balanced, "stack.empresa", "mode.empresa"),
            "pruebas" => (UserProfile.Developer, "stack.pruebas", "mode.pruebas"),
            _ => (UserProfile.Balanced, "privacy.lab", "mode.balanced")
        };

        if (mode is not ("gaming" or "optimized" or "developer" or "balanced" or "empresa" or "pruebas"))
        {
            HomeModeStatus.Text = "Unknown mode: " + mode;
            return;
        }

        var label = Loc.T(labelKey);

        _suppressProfile = true;
        SelectProfileBox(profile switch
        {
            UserProfile.Gaming => "gaming",
            UserProfile.Developer => "developer",
            UserProfile.Virtualization => "virtualization",
            UserProfile.Balanced => "balanced",
            _ => "balanced"
        });
        _suppressProfile = false;

        HomeModeStatus.Text = Loc.IsEnglish
            ? $"Mode: {label} → pack {presetId}"
            : $"Modo: {label} → pack {presetId}";

        SelectNav("config");
        Reload(profile, presetId);
    }

    private void RenderPresets(string? selectPresetId = null)
    {
        if (_session is null)
        {
            return;
        }

        var picks = _session.PresetEvals.Select(e => new PresetPick { Eval = e }).ToList();
        PresetList.ItemsSource = picks;
        if (picks.Count == 0)
        {
            return;
        }

        if (selectPresetId is not null)
        {
            var match = picks.FindIndex(p => string.Equals(p.Eval.Preset.Id, selectPresetId, StringComparison.OrdinalIgnoreCase));
            PresetList.SelectedIndex = match >= 0 ? match : 0;
        }
        else
        {
            PresetList.SelectedIndex = 0;
        }
    }

    private void PresetList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetList.SelectedItem is not PresetPick pick)
        {
            return;
        }

        var eval = pick.Eval;
        PresetTitle.Text = eval.Preset.Title;
        PresetSummary.Text = eval.Preset.Summary;
        PresetProgress.Text = eval.Total == 0
            ? "Marca los tweaks que quieras en Configuración específica."
            : $"{eval.ReadyCount} listos · {eval.GapCount} pendientes · {eval.Items.Count} filas";
        PresetSimulateText.Text = "";
        var custom = eval.Preset.IsCustom;
        PresetGrid.ItemsSource = eval.Items.Select(i => new PresetItemRow
        {
            Include = custom ? i.Estado == "FALTA" : i.Kind != "step",
            Id = i.Id,
            Estado = i.Estado,
            Title = i.Title,
            Actual = i.Actual,
            Desired = i.Desired,
            HowTo = i.HowTo,
            SettingsUri = i.SettingsUri
        }).ToList();
        Persist();
    }

    private void HomePreset_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PresetPick pick)
        {
            return;
        }

        SelectNav("config");
        RenderPresets(pick.Eval.Preset.Id);
    }

    private void ActivateRow_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is PresetItemRow row)
        {
            Launch(row.SettingsUri, "Esta fila no tiene enlace. Sigue la columna Cómo.");
        }
    }
}

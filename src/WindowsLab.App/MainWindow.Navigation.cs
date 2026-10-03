using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public partial class MainWindow
{
    private List<TweakRow> _allTweakRows = [];
    private string? _lastMontageId;
    private HashSet<string> _selectedTweakIds = new(StringComparer.OrdinalIgnoreCase);

    private void Nav_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageHome is null || PageAdjust is null || PageApps is null || PagePerformance is null || PageMore is null
            || PageWorkloads is null || PageBackups is null || PageTools is null)
        {
            return;
        }

        if (Nav.SelectedItem is not ListBoxItem item || item.Tag is not string tag || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        ShowPage(tag);
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
        PageAdjust.Visibility = tag == "adjust" ? Visibility.Visible : Visibility.Collapsed;
        PageApps.Visibility = tag == "apps" ? Visibility.Visible : Visibility.Collapsed;
        PagePerformance.Visibility = tag == "performance" ? Visibility.Visible : Visibility.Collapsed;
        PageWorkloads.Visibility = tag == "workloads" ? Visibility.Visible : Visibility.Collapsed;
        PageBackups.Visibility = tag == "backups" ? Visibility.Visible : Visibility.Collapsed;
        PageTools.Visibility = tag == "tools" ? Visibility.Visible : Visibility.Collapsed;
        PageMore.Visibility = tag == "more" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "adjust")
        {
            Dispatcher.BeginInvoke(UpdateAdjustCategoryColumns, DispatcherPriority.Loaded);
        }

        if (tag == "workloads")
        {
            RenderWorkloads();
        }

        if (tag == "backups")
        {
            RefreshBackupList();
        }

        if (tag == "tools")
        {
            RenderChannels();
            RenderSystem();
            RenderSecurity();
            RefreshOverlayRecommendation();
        }
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
                ? (Loc.IsEnglish ? $"C: {freeGb:0.0} GB free — low space." : $"C: {freeGb:0.0} GB libres — espacio bajo.")
                : "";
        }
        else
        {
            HomeWarn.Text = "";
        }

        var settings = OperatorSettingsStore.Load();
        var gateOn = SystemApplyPolicy.IsAllowed(settings, iAmOnLabVmFlag: false);
        HomeGateLine.Text = Loc.T("home.gate")
            .Replace("{0}", Loc.T(gateOn ? "home.gate.on" : "home.gate.off"), StringComparison.Ordinal);

        UpdateModeButton(BtnModeGamingStat, "gaming");
        UpdateModeButton(BtnModeOptimizedStat, "perf.max");
        UpdateModeButton(BtnModeDevStat, "developer");
        UpdateModeButton(BtnModeWorkStat, "work.focus");
        UpdateModeButton(BtnModeBalancedStat, "privacy.lab");
        UpdateModeButton(BtnModeEmpresaStat, "stack.empresa");
        UpdateModeButton(BtnModePruebasStat, "stack.pruebas");

        HomeCapabilitiesTitle.Text = Loc.T("home.capabilities");
        HomeCapabilitiesHint.Text = Loc.T("home.capabilitiesHint");
        CapAuditTitle.Text = Loc.T("home.cap.audit");
        CapAuditBody.Text = Loc.T("home.cap.auditBody");
        CapTweaksTitle.Text = Loc.T("home.cap.tweaks");
        CapTweaksBody.Text = Loc.T("home.cap.tweaksBody");
        CapAppsTitle.Text = Loc.T("home.cap.apps");
        CapAppsBody.Text = Loc.T("home.cap.appsBody");
        CapPerfTitle.Text = Loc.T("home.cap.perf");
        CapPerfBody.Text = Loc.T("home.cap.perfBody");
        CapLoadsTitle.Text = Loc.T("home.cap.loads");
        CapLoadsBody.Text = Loc.T("home.cap.loadsBody");
        CapBackupsTitle.Text = Loc.T("home.cap.backups");
        CapBackupsBody.Text = Loc.T("home.cap.backupsBody");

        RenderPending();
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
            stat.Text = Loc.IsEnglish ? "Missing" : "No encontrado";
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
            "work" => (UserProfile.Virtualization, "work.focus", "mode.work"),
            "balanced" => (UserProfile.Balanced, "privacy.lab", "mode.balanced"),
            "empresa" => (UserProfile.Balanced, "stack.empresa", "mode.empresa"),
            "pruebas" => (UserProfile.Developer, "stack.pruebas", "mode.pruebas"),
            _ => (UserProfile.Balanced, "privacy.lab", "mode.balanced")
        };

        var label = Loc.T(labelKey);

        _suppressProfile = true;
        SelectProfileBox(profile switch
        {
            UserProfile.Gaming => "gaming",
            UserProfile.Developer => "developer",
            UserProfile.Virtualization => "virtualization",
            _ => "balanced"
        });
        _suppressProfile = false;

        HomeModeStatus.Text = Loc.IsEnglish
            ? $"Setup: {label} → selecting on Tweaks"
            : $"Montaje: {label} → marcando en Ajustes";

        _lastMontageId = presetId;
        SelectNav("adjust");
        if (_session.Profile != profile)
        {
            Reload(profile, presetId);
        }
        else
        {
            SelectMontage(presetId);
        }
    }

    private void BuildAdjustPresetBar()
    {
        AdjustPresetBar.Children.Clear();
        if (_session is null)
        {
            return;
        }

        foreach (var eval in _session.PresetEvals.Where(p => !p.Preset.IsCustom).OrderBy(p => p.Preset.Order))
        {
            var pick = new PresetPick { Eval = eval };
            var btn = new Button
            {
                Content = pick.Label,
                Tag = pick,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(10, 6, 10, 6),
                ToolTip = eval.Preset.Summary
            };
            btn.Click += MontagePreset_OnClick;
            AdjustPresetBar.Children.Add(btn);
        }
    }

    private void MontagePreset_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PresetPick pick)
        {
            return;
        }

        SelectMontage(pick.Eval.Preset.Id);
    }

    private void SelectMontage(string presetId)
    {
        if (_session is null)
        {
            return;
        }

        _lastMontageId = presetId;
        var eval = _session.PresetEvals.FirstOrDefault(p =>
            string.Equals(p.Preset.Id, presetId, StringComparison.OrdinalIgnoreCase));
        _selectedTweakIds = new HashSet<string>(
            eval?.Preset.TweakIds ?? [],
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in _allTweakRows)
        {
            row.IsSelected = _selectedTweakIds.Contains(row.Id);
        }

        RefreshTweakCategoryView();
        AdjustStatus.Text = Loc.IsEnglish
            ? $"Setup «{eval?.Preset.Title ?? presetId}» — {_selectedTweakIds.Count} checked. Review and Apply."
            : $"Montaje «{eval?.Preset.Title ?? presetId}» — {_selectedTweakIds.Count} marcados. Revisa y Aplicar.";
        Persist();
    }

    private void RenderTweaks()
    {
        if (_session is null)
        {
            return;
        }

        var recommended = new HashSet<string>(
            _session.Recommendations.Select(r => r.TweakId),
            StringComparer.OrdinalIgnoreCase);

        _allTweakRows = _session.Catalog.Select(t =>
        {
            var d = _session.Detections.FirstOrDefault(x => x.TweakId == t.Id);
            var (pros, cons) = TweakRow.BuildImpact(t, Loc.IsEnglish);
            return new TweakRow
            {
                IsSelected = _selectedTweakIds.Contains(t.Id),
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                ProsText = pros,
                ConsText = cons,
                Category = t.Category,
                Risk = t.Risk.ToString(),
                Evidence = t.Evidence.ToString(),
                Actual = d?.ActualDisplay,
                Desired = d?.DesiredDisplay,
                Match = d?.MatchesDesired,
                Status = d?.Status.ToString(),
                IsRecommended = recommended.Contains(t.Id),
                AffectsSecurity = t.AffectsSecurity
            };
        }).ToList();

        BuildAdjustPresetBar();
        if (_lastMontageId is not null)
        {
            SelectMontage(_lastMontageId);
        }
        else
        {
            RefreshTweakCategoryView();
        }
    }

    private void RefreshTweakCategoryView()
    {
        var search = AdjustSearchBox?.Text?.Trim() ?? "";
        var filter = (AdjustFilterBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";

        IEnumerable<TweakRow> q = _allTweakRows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(r =>
                r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || r.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                || r.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        q = filter switch
        {
            "gaps" => q.Where(r => r.Match is not true),
            "recommended" => q.Where(r => r.IsRecommended),
            _ => q
        };

        var groups = q
            .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => CategorySortKey(g.Key))
            .Select(g =>
            {
                var rows = g.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase).ToList();
                return new TweakCategoryGroup
                {
                    Category = g.Key,
                    Header = $"{CategoryLabel(g.Key)} ({rows.Count})",
                    Rows = rows
                };
            })
            .ToList();

        TweakCategoryList.ItemsSource = groups;
        TweakCategoryList.Dispatcher.BeginInvoke(UpdateAdjustCategoryColumns, DispatcherPriority.Loaded);
        UpdateAdjustSelectionStatus();
    }

    private void TweakSelect_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        CaptureSelectionsFromUi();
        UpdateAdjustSelectionStatus();
    }

    private void TweakInfo_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TweakRow row)
        {
            return;
        }

        MessageBox.Show(
            $"{row.Title}\n\n{row.Description}\n\n{row.ProsText}\n\n{row.ConsText}\n\n"
            + $"{row.Id} · {row.Evidence} · {row.Risk}\n"
            + (Loc.IsEnglish ? "Actual → Desired: " : "Actual → Objetivo: ")
            + $"{row.Actual ?? "?"} → {row.Desired ?? "?"}",
            Loc.IsEnglish ? "Tweak details" : "Detalle del ajuste",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateAdjustSelectionStatus()
    {
        var selected = _allTweakRows.Where(r => r.IsSelected).ToList();
        var match = selected.Count(r => r.Match is true);
        var gap = selected.Count - match;
        AdjustStatus.Text = Loc.T("adjust.selected")
            .Replace("{0}", selected.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{1}", gap.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{2}", match.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private void PageAdjust_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateAdjustCategoryColumns, DispatcherPriority.Background);

    private void UpdateAdjustCategoryColumns()
    {
        if (TweakCategoryList is null || PageAdjust is null || PageAdjust.ActualWidth <= 0)
        {
            return;
        }

        var panel = FindVisualDescendant<WrapPanel>(TweakCategoryList);
        if (panel is null)
        {
            return;
        }

        // Two cards across on ~1080p content; one column when the pane is narrow.
        // Card Border already has 10px right/bottom margin for gutters.
        var available = Math.Max(280, PageAdjust.ActualWidth - 20);
        var columns = available >= 980 ? 2 : 1;
        var cardWidth = columns == 1
            ? available
            : Math.Floor(available / 2);

        foreach (UIElement child in panel.Children)
        {
            if (child is FrameworkElement fe)
            {
                fe.Width = cardWidth;
            }
        }
    }

    private static T? FindVisualDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static int CategorySortKey(string category) => category.ToLowerInvariant() switch
    {
        "privacy" => 10,
        "explorer" => 20,
        "taskbar" => 30,
        "desktop" => 40,
        "keyboard" => 50,
        "gaming" => 60,
        "developer" => 70,
        "power" => 80,
        "network" => 90,
        "security" => 100,
        _ => 200
    };

    private static string CategoryLabel(string category)
    {
        var key = "cat." + category.ToLowerInvariant();
        var loc = Loc.T(key);
        return loc == key ? category : loc;
    }

    private void AdjustSearch_OnChanged(object sender, TextChangedEventArgs e) => RefreshTweakCategoryView();

    private void AdjustFilter_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RefreshTweakCategoryView();
    }

    private void AdjustClear_OnClick(object sender, RoutedEventArgs e)
    {
        _selectedTweakIds.Clear();
        _lastMontageId = null;
        foreach (var row in _allTweakRows)
        {
            row.IsSelected = false;
        }

        RefreshTweakCategoryView();
        AdjustStatus.Text = Loc.IsEnglish ? "Cleared." : "Limpiado.";
    }

    private void CaptureSelectionsFromUi()
    {
        _selectedTweakIds = new HashSet<string>(
            _allTweakRows.Where(r => r.IsSelected).Select(r => r.Id),
            StringComparer.OrdinalIgnoreCase);
    }

    private async void AdjustApply_OnClick(object sender, RoutedEventArgs e)
    {
        CaptureSelectionsFromUi();
        var ids = _selectedTweakIds.ToArray();
        await ApplyTweaksById(ids, status => AdjustStatus.Text = status).ConfigureAwait(true);
    }

    private void AdjustSimulate_OnClick(object sender, RoutedEventArgs e)
    {
        CaptureSelectionsFromUi();
        if (_session is null)
        {
            AdjustStatus.Text = Loc.IsEnglish ? "Still loading." : "Aún cargando.";
            return;
        }

        if (_selectedTweakIds.Count == 0)
        {
            AdjustStatus.Text = Loc.IsEnglish ? "Check at least one tweak." : "Marca al menos un ajuste.";
            return;
        }

        var match = 0;
        var gap = 0;
        foreach (var id in _selectedTweakIds)
        {
            var d = _session.Detections.FirstOrDefault(x => x.TweakId == id);
            if (d?.MatchesDesired == true)
            {
                match++;
            }
            else
            {
                gap++;
            }
        }

        AdjustStatus.Text = Loc.IsEnglish
            ? $"Simulate (0 writes): {_selectedTweakIds.Count} selected, {match} already match, {gap} gaps."
            : $"Simulación (0 escrituras): {_selectedTweakIds.Count} seleccionados, {match} ya OK, {gap} pendientes.";
    }
}

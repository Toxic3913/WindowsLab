using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public sealed class PresetPick
{
    public required PresetEvaluation Eval { get; init; }

    public string Label => Eval.Total == 0
        ? $"{Eval.Preset.Title}  (elige ítems)"
        : $"{Eval.Preset.Title}  ({Eval.ReadyCount}/{Eval.Total} listos)";
}

public sealed class PresetItemRow
{
    public bool Include { get; set; }
    public string Id { get; init; } = "";
    public string Estado { get; init; } = "";
    public string Title { get; init; } = "";
    public string Actual { get; init; } = "";
    public string Desired { get; init; } = "";
    public string HowTo { get; init; } = "";
    public string? SettingsUri { get; init; }
}

public partial class MainWindow : Window
{
    private LabSession _session = null!;

    public MainWindow()
    {
        InitializeComponent();
        Reload(UserProfile.Balanced);
    }

    private void Reload(UserProfile profile)
    {
        _session = new LabSession(profile);
        RenderHome();
        RenderPresets();
        RenderSystem();
        RenderSecurity();
        RenderTweaks();
        RenderAdvice();
        RenderChecklist();
        SettingsCatalogPath.Text = "Catálogo: " + (CatalogLocator.FindTweaksDirectory() ?? "(no encontrado)")
            + "  ·  Checklist: " + (CatalogLocator.FindChecklistsDirectory() ?? "(no encontrado)")
            + "  ·  Packs: " + (CatalogLocator.FindPresetsDirectory() ?? "(no encontrado)");
    }

    private void ProfileBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var item = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "balanced";
        var profile = item.ToLowerInvariant() switch
        {
            "developer" => UserProfile.Developer,
            "gaming" => UserProfile.Gaming,
            "virtualization" => UserProfile.Virtualization,
            _ => UserProfile.Balanced
        };
        Reload(profile);
    }

    private void Nav_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageHome is null || PageConfig is null)
        {
            return;
        }

        var index = Nav.SelectedIndex;
        PageHome.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageConfig.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageSystem.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageSecurity.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageTweaks.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        PageAdvice.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
        PageChecklist.Visibility = index == 6 ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = index == 7 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderHome()
    {
        var inv = _session.Inventory;
        HomeSummary.Text =
            $"{inv.Os.FamilyLabel} {inv.Os.DisplayVersion} (build {inv.Os.Build})  ·  {inv.Cpu.Name}  ·  {inv.Ram.TotalBytes / (1024d * 1024 * 1024):0.0} GB RAM  ·  {_session.Catalog.Count} tweaks  ·  {_session.Presets.Count} packs  ·  {_session.ChecklistResults.Count(c => c.Verdict == ChecklistVerdict.Gap)} huecos en checklist";

        var c = _session.SystemVolume;
        if (c is not null)
        {
            var freeGb = c.FreeBytes / (1024d * 1024 * 1024);
            HomeWarn.Text = freeGb < 20
                ? $"C: tiene {freeGb:0.0} GB libres. Los puntos de restauración pueden fallar. WindowsLab no mueve VSS en Beta 0."
                : $"C: {freeGb:0.0} GB libres. Usa Configurar para packs (rendimiento, privacidad, escritorio). Apply sigue bloqueado.";
        }
        else
        {
            HomeWarn.Text = "";
        }

        HomePresetList.ItemsSource = _session.PresetEvals
            .Where(p => !p.Preset.IsCustom)
            .Select(p => $"{p.Preset.Title}: {p.ReadyCount}/{p.Total} listos, {p.GapCount} pendientes")
            .ToArray();
        ProbeList.ItemsSource = inv.Probes.Select(p => $"{p.Status,-12} {p.ProbeId}  {p.Message}").ToArray();
    }

    private void RenderPresets()
    {
        var picks = _session.PresetEvals.Select(e => new PresetPick { Eval = e }).ToList();
        PresetList.ItemsSource = picks;
        if (picks.Count > 0)
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
    }

    private IEnumerable<PresetItemRow> SelectedRows() =>
        PresetGrid.Items.OfType<PresetItemRow>().Where(r => r.Include);

    private void OpenPresetSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var uri = (PresetList.SelectedItem as PresetPick)?.Eval.Preset.SettingsUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            uri = SelectedRows().Select(r => r.SettingsUri).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        }

        Launch(uri, "Este pack no tiene página de Configuración asociada.");
    }

    private void OpenSelectedSetting_OnClick(object sender, RoutedEventArgs e)
    {
        if (PresetGrid.SelectedItem is PresetItemRow row)
        {
            Launch(row.SettingsUri, "Esta fila no tiene enlace. Sigue la columna Cómo.");
            return;
        }

        PresetSimulateText.Text = "Selecciona una fila de la tabla.";
    }

    private void SimulatePreset_OnClick(object sender, RoutedEventArgs e)
    {
        var rows = SelectedRows().ToArray();
        if (rows.Length == 0)
        {
            PresetSimulateText.Text = "Marca al menos un ítem (columna Incluir).";
            return;
        }

        var ready = rows.Count(r => r.Estado == "OK");
        var gap = rows.Count(r => r.Estado == "FALTA");
        PresetSimulateText.Text =
            $"Simulación (0 escrituras): {rows.Length} ítems, {ready} ya coinciden, {gap} pendientes. Abre cada FALTA con «Abrir ajuste seleccionado». Apply pack: bloqueado.";
    }

    private void Launch(string? target, string ifMissing)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            PresetSimulateText.Text = ifMissing;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            PresetSimulateText.Text = "Abierto: " + target;
        }
        catch (Exception ex)
        {
            PresetSimulateText.Text = "No se pudo abrir " + target + ": " + ex.Message;
        }
    }

    private void RenderSystem()
    {
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
        SecurityPanel.Children.Clear();
        var s = _session.Inventory.Security;
        AddLine(SecurityPanel, $"VBS (Device Guard): {s.VirtualizationBasedSecurityStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "denied/unknown"}");
        AddLine(SecurityPanel, $"Defender AntivirusEnabled: {Fmt(s.DefenderEnabled)}  RTP: {Fmt(s.RealTimeProtection)}");
        AddLine(SecurityPanel, $"Firewall (perfil estándar): {Fmt(s.FirewallEnabled)}");
        AddLine(SecurityPanel, s.Notes ?? "Sin notas.");
        AddLine(SecurityPanel, "Beta 0 no recomienda desactivar Defender, HVCI ni Secure Boot.");
    }

    private void RenderTweaks()
    {
        var rows = _session.Catalog.Select(t =>
        {
            var d = _session.Detections.FirstOrDefault(x => x.TweakId == t.Id);
            return new
            {
                t.Id,
                t.Title,
                t.Category,
                t.Risk,
                t.Evidence,
                Actual = d?.ActualDisplay,
                Desired = d?.DesiredDisplay,
                Match = d?.MatchesDesired,
                d?.Status
            };
        }).ToList();
        TweakGrid.ItemsSource = rows;
    }

    private void RenderAdvice()
    {
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

    private void RenderChecklist()
    {
        var rows = _session.ChecklistResults.Select(r => new
        {
            Estado = r.Verdict switch
            {
                ChecklistVerdict.Ok => "OK",
                ChecklistVerdict.Gap => "FALTA",
                ChecklistVerdict.Unknown => "?",
                _ => "INFO"
            },
            r.Section,
            r.Title,
            Actual = r.Actual,
            Desired = r.Desired,
            r.Note,
            Cómo = r.HowTo
        }).ToList();
        ChecklistGrid.ItemsSource = rows;
        var ok = rows.Count(x => x.Estado == "OK");
        var gap = rows.Count(x => x.Estado == "FALTA");
        var info = rows.Count(x => x.Estado == "INFO");
        var unk = rows.Count(x => x.Estado == "?");
        ChecklistSummary.Text = $"OK {ok}  ·  FALTA {gap}  ·  INFO {info}  ·  ? {unk}  ·  total {rows.Count}";
    }

    private void Simulate_OnClick(object sender, RoutedEventArgs e)
    {
        if (TweakGrid.SelectedItem is null)
        {
            SimulateText.Text = "Selecciona un tweak.";
            return;
        }

        var id = TweakGrid.SelectedItem.GetType().GetProperty("Id")?.GetValue(TweakGrid.SelectedItem)?.ToString();
        var detection = _session.Detections.FirstOrDefault(d => d.TweakId == id);
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

using System.Windows;
using System.Windows.Controls;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class MainWindow
{
    private IReadOnlyList<ExternalWorkloadDefinition> _workloads = [];

    private void EnsureWorkloadsLoaded()
    {
        if (_workloads.Count > 0)
        {
            return;
        }

        var dir = CatalogLocator.FindWorkloadsDirectory();
        if (dir is null)
        {
            return;
        }

        try
        {
            _workloads = ExternalWorkloadCatalog.LoadDirectory(dir);
        }
        catch (Exception ex)
        {
            WorkloadStatusText.Text = Truncate(ex.Message, 200);
        }
    }

    private void RenderWorkloads()
    {
        EnsureWorkloadsLoaded();
        if (WorkloadPanel is null)
        {
            return;
        }

        WorkloadPanel.Children.Clear();
        if (_workloads.Count == 0)
        {
            WorkloadStatusText.Text = Loc.IsEnglish
                ? "No workload catalog found."
                : "No hay catálogo de cargas externas.";
            return;
        }

        foreach (var def in _workloads)
        {
            var status = ExternalWorkloadController.Detect(def);
            var card = new Border
            {
                Style = (Style)FindResource("AdjustCategoryCard"),
                Margin = new Thickness(0, 0, 10, 10),
                MinWidth = 260,
                MaxWidth = 360
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = def.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            });
            stack.Children.Add(new TextBlock
            {
                Text = status.Summary,
                Margin = new Thickness(0, 4, 0, 6),
                Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            var btn = new Button
            {
                Content = Loc.IsEnglish ? "Stop" : "Detener",
                Tag = def,
                IsEnabled = status.Active,
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            if (status.Active)
            {
                btn.Style = (Style)FindResource("PrimaryButton");
            }

            btn.Click += WorkloadStop_OnClick;
            stack.Children.Add(btn);
            card.Child = stack;
            WorkloadPanel.Children.Add(card);
        }

        var active = _workloads.Count(w => ExternalWorkloadController.Detect(w).Active);
        WorkloadStatusText.Text = Loc.IsEnglish
            ? $"{active} active · {_workloads.Count} curated (Steam, Riot, Overwolf, …)"
            : $"{active} activas · {_workloads.Count} curadas (Steam, Riot, Overwolf, …)";
    }

    private void WorkloadRefresh_OnClick(object sender, RoutedEventArgs e) => RenderWorkloads();

    private void WorkloadStop_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ExternalWorkloadDefinition def)
        {
            return;
        }

        var status = ExternalWorkloadController.Detect(def);
        if (!status.Active)
        {
            WorkloadStatusText.Text = Loc.IsEnglish ? "Already idle." : "Ya está parado.";
            RenderWorkloads();
            return;
        }

        var confirm = MessageBox.Show(
            Loc.IsEnglish
                ? $"Stop «{def.Title}»?\n{status.Summary}\n\nCloses allowlisted processes/services only. Does not touch Defender/Search/SysMain."
                : $"¿Detener «{def.Title}»?\n{status.Summary}\n\nSolo procesos/servicios de la lista. No toca Defender/Search/SysMain.",
            "WindowsLab",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var result = ExternalWorkloadController.Stop(def);
        WorkloadStatusText.Text = result.Ok
            ? (Loc.IsEnglish
                ? $"Stopped {result.ProcessesStopped} proc / {result.ServicesStopped} svc — {def.Title}"
                : $"Detenidos {result.ProcessesStopped} proc / {result.ServicesStopped} svc — {def.Title}")
            : Truncate(string.Join(" · ", result.Messages), 280);
        RenderWorkloads();
    }

    private void WorkloadStopAllActive_OnClick(object sender, RoutedEventArgs e)
    {
        EnsureWorkloadsLoaded();
        var active = _workloads.Where(w => ExternalWorkloadController.Detect(w).Active).ToArray();
        if (active.Length == 0)
        {
            WorkloadStatusText.Text = Loc.IsEnglish ? "Nothing active." : "Nada activo.";
            return;
        }

        var names = string.Join(", ", active.Select(a => a.Title));
        var confirm = MessageBox.Show(
            Loc.IsEnglish
                ? $"Stop all active curated workloads?\n{names}"
                : $"¿Detener todas las cargas activas?\n{names}",
            "WindowsLab",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var proc = 0;
        var svc = 0;
        foreach (var def in active)
        {
            var r = ExternalWorkloadController.Stop(def);
            proc += r.ProcessesStopped;
            svc += r.ServicesStopped;
        }

        WorkloadStatusText.Text = Loc.IsEnglish
            ? $"Batch stop: {proc} proc / {svc} svc"
            : $"Lote: {proc} proc / {svc} svc";
        RenderWorkloads();
    }
}

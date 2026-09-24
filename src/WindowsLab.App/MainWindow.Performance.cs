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
    private void OpenPerformance_OnClick(object sender, RoutedEventArgs e) => SelectNav("performance");

    private void PerfGroupFilter_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || PerfProcessGrid is null)
        {
            return;
        }

        ApplyPerfProcessFilter();
    }

    private void UpdatePerfFilterLabels()
    {
        if (PerfGroupFilter is null)
        {
            return;
        }

        foreach (ComboBoxItem item in PerfGroupFilter.Items)
        {
            var tag = item.Tag as string ?? "";
            item.Content = tag switch
            {
                "windows" => Loc.T("perf.group.windows"),
                "microsoft" => Loc.T("perf.group.microsoft"),
                "external" => Loc.T("perf.group.external"),
                _ => Loc.T("perf.group.all")
            };
        }
    }

    private void RefreshPerformanceDashboard()
    {
        if (_perfBusy || PagePerformance is null || PagePerformance.Visibility != Visibility.Visible)
        {
            return;
        }

        _perfBusy = true;
        Task.Run(() => LiveSystemReader.ReadDashboard(includeProcesses: true, processTopN: 25)).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                _perfBusy = false;
                if (PagePerformance.Visibility != Visibility.Visible)
                {
                    return;
                }

                if (t.IsFaulted)
                {
                    PerfMeta.Text = Truncate(t.Exception?.GetBaseException().Message ?? "error", 300);
                    return;
                }

                ApplyDashboardToUi(t.Result);
            });
        }, TaskScheduler.Default);
    }

    private void ApplyDashboardToUi(LiveDashboardSnapshot dash)
    {
        PerfCpuValue.Text = $"{dash.CpuPercent:0.0}%";
        var ram = dash.Ram;
        var commit = ram.CommitPercent is null
            ? ""
            : Loc.IsEnglish
                ? $" · commit {ram.CommitPercent:0.0}%"
                : $" · commit {ram.CommitPercent:0.0}%";
        PerfRamValue.Text =
            $"{ram.UsedGb:0.0}/{ram.TotalGb:0.0} GB ({ram.Percent:0.0}%)" +
            $"\n{Loc.T("perf.ram.avail")}: {ram.AvailableGb:0.0} GB{commit}";

        PerfDiskValue.Text = dash.Disks.Count == 0
            ? "—"
            : string.Join("\n", dash.Disks.Select(d =>
                $"{d.Root} {d.FreeGb:0.0}/{d.TotalGb:0.0} GB ({d.FreePercent:0.0}% {Loc.T("perf.disk.free")})"));

        var io = dash.DiskIo.PercentDiskTime is null
            ? ""
            : $" · disk time {dash.DiskIo.PercentDiskTime:0.0}% q={dash.DiskIo.AvgQueueLength:0.00}";
        var totals = dash.Processes is null
            ? ""
            : string.Join(" · ", dash.Processes.GroupTotals.Select(g =>
                $"{GroupLabel(g.Group)}={g.Count} ({FormatBytes(g.WorkingSetBytes)})"));
        PerfMeta.Text = $"{dash.CapturedUtc:HH:mm:ss} · {dash.OsLine} · IP {dash.NetworkLine}{io}\n{totals}";

        _perfRows = (dash.Processes?.TopByWorkingSet ?? [])
            .Select(r => new PerfProcessRow
            {
                Id = r.Id,
                Name = r.Name,
                Group = r.Group,
                GroupLabel = GroupLabel(r.Group),
                CpuText = $"{r.CpuPercent:0.0}",
                WorkingSetText = FormatBytes(r.WorkingSetBytes),
                PrivateText = FormatBytes(r.PrivateBytes),
                Path = r.Path ?? ""
            })
            .ToArray();
        ApplyPerfProcessFilter();
    }

    private void ApplyPerfProcessFilter()
    {
        var tag = (PerfGroupFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        IEnumerable<PerfProcessRow> rows = _perfRows;
        rows = tag switch
        {
            "windows" => rows.Where(r => r.Group == ProcessGroup.Windows),
            "microsoft" => rows.Where(r => r.Group == ProcessGroup.Microsoft),
            "external" => rows.Where(r => r.Group == ProcessGroup.External),
            _ => rows
        };
        PerfProcessGrid.ItemsSource = rows.ToArray();
    }

    private static string GroupLabel(ProcessGroup g) => g switch
    {
        ProcessGroup.Windows => Loc.T("perf.group.windows"),
        ProcessGroup.Microsoft => Loc.T("perf.group.microsoft"),
        ProcessGroup.External => Loc.T("perf.group.external"),
        _ => throw new ArgumentOutOfRangeException(nameof(g), g, null)
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return $"{kb:0.0} KB";
        }

        var mb = kb / 1024d;
        if (mb < 1024)
        {
            return $"{mb:0.0} MB";
        }

        return $"{mb / 1024d:0.00} GB";
    }
}

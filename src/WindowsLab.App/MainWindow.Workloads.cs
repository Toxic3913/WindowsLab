using System.Windows;
using System.Windows.Controls;
using WindowsLab.Applications;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class MainWindow
{
    private const string MemReductHubId = "hub.performance.memreduct";
    private const string MemReductAppId = "app.tool.memreduct";

    private IReadOnlyList<ExternalWorkloadDefinition> _workloads = [];
    private bool _workloadProcessBusy;
    private SessionProcessRow[] _workloadProcRows = [];
    private SessionServiceRow[] _workloadSvcRows = [];

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
                FontSize = 14,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush")
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

    private void WorkloadRefresh_OnClick(object sender, RoutedEventArgs e)
    {
        RenderWorkloads();
        RefreshWorkloadProcessListAsync();
    }

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
        RefreshWorkloadProcessListAsync();
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
        RefreshWorkloadProcessListAsync();
    }

    private void WorkloadProcessRefresh_OnClick(object sender, RoutedEventArgs e) =>
        RefreshWorkloadProcessListAsync();

    private void WorkloadProcessSearch_OnChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ApplyWorkloadBackgroundFilter();

    private void RefreshWorkloadProcessListAsync()
    {
        if (WorkloadProcessList is null || WorkloadServiceList is null || _workloadProcessBusy)
        {
            return;
        }

        _workloadProcessBusy = true;
        if (WorkloadProcessStatus is not null)
        {
            WorkloadProcessStatus.Text = Loc.T("workload.proc.loading");
        }

        Task.Run(() =>
        {
            var procs = SessionResourceController.ListProcesses(500);
            var svcs = SessionResourceController.ListServices(400);
            return (procs, svcs);
        }).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                _workloadProcessBusy = false;
                if (WorkloadProcessList is null || WorkloadServiceList is null)
                {
                    return;
                }

                if (t.IsFaulted)
                {
                    if (WorkloadProcessStatus is not null)
                    {
                        WorkloadProcessStatus.Text = Truncate(t.Exception?.GetBaseException().Message ?? "error", 240);
                    }

                    return;
                }

                var (procs, svcs) = t.Result;
                _workloadProcRows = procs.Select(MapProcessRow).ToArray();
                _workloadSvcRows = svcs.Select(MapServiceRow).ToArray();
                ApplyWorkloadBackgroundFilter();
            });
        }, TaskScheduler.Default);
    }

    private void ApplyWorkloadBackgroundFilter()
    {
        if (WorkloadProcessList is null || WorkloadServiceList is null)
        {
            return;
        }

        var q = WorkloadProcessSearch?.Text?.Trim() ?? "";
        IEnumerable<SessionProcessRow> procs = _workloadProcRows;
        IEnumerable<SessionServiceRow> svcs = _workloadSvcRows;
        if (!string.IsNullOrWhiteSpace(q))
        {
            procs = procs.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Detail.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Path.Contains(q, StringComparison.OrdinalIgnoreCase));
            svcs = svcs.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Detail.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var procArr = procs.ToArray();
        var svcArr = svcs.ToArray();
        // Drop selection on rows hidden by the filter so Stop cannot act on invisible checks.
        var visibleProc = new HashSet<SessionProcessRow>(procArr);
        var visibleSvc = new HashSet<SessionServiceRow>(svcArr);
        foreach (var r in _workloadProcRows)
        {
            if (r.IsSelected && !visibleProc.Contains(r))
            {
                r.IsSelected = false;
            }
        }

        foreach (var r in _workloadSvcRows)
        {
            if (r.IsSelected && !visibleSvc.Contains(r))
            {
                r.IsSelected = false;
            }
        }

        WorkloadProcessList.ItemsSource = procArr;
        WorkloadServiceList.ItemsSource = svcArr;
        if (WorkloadProcessStatus is not null)
        {
            WorkloadProcessStatus.Text = Loc.T("workload.bg.count")
                .Replace("{0}", procArr.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{1}", procArr.Count(r => r.CanSelect).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{2}", svcArr.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{3}", svcArr.Count(r => r.CanSelect).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
    }

    private static SessionProcessRow MapProcessRow(SessionProcessInfo p)
    {
        // Prefer live window title when present; FileDescription is the fallback for tray apps.
        var title = FirstNonEmpty(p.WindowTitle, p.DisplayName, p.Name) ?? p.Name;
        var detailParts = new List<string> { p.Name, "PID " + p.Id, GroupLabel(p.Group) };
        if (p.IsBackgroundInterest && !p.IsBlocked)
        {
            detailParts.Add(Loc.T("workload.bg.tag"));
        }

        // External apps show OK (stoppable); token matches on Windows/Microsoft show Fondo; hard-blocks stay Bloqueado.
        var statusKind = p.IsBlocked
            ? "blocked"
            : p.IsBackgroundInterest && p.Group != ProcessGroup.External
                ? "bg"
                : "ok";
        var blockLabel = p.IsBlocked
            ? Loc.T("game.session.blocked")
            : statusKind == "bg"
                ? Loc.T("workload.proc.bgReady")
                : Loc.T("game.session.ok");
        var initial = string.IsNullOrWhiteSpace(title) ? "?" : title.Trim()[0].ToString().ToUpperInvariant();
        return new SessionProcessRow
        {
            Id = p.Id,
            Name = p.Name,
            Title = title,
            Detail = string.Join(" · ", detailParts),
            WorkingSetText = FormatBytes(p.WorkingSetBytes),
            WorkingSetBytes = p.WorkingSetBytes,
            GroupLabel = GroupLabel(p.Group),
            Path = p.Path ?? "",
            PathShort = ShortenPath(p.Path),
            Initial = initial,
            IsBlocked = p.IsBlocked,
            BlockLabel = blockLabel,
            StatusKind = statusKind
        };
    }

    private static SessionServiceRow MapServiceRow(SessionServiceInfo s)
    {
        var initial = string.IsNullOrWhiteSpace(s.DisplayName)
            ? "?"
            : s.DisplayName.Trim()[0].ToString().ToUpperInvariant();
        var statusKind = s.IsBlocked
            ? "blocked"
            : s.IsRunning
                ? "ok"
                : "start";
        var action = s.IsBlocked
            ? Loc.T("game.session.blocked")
            : s.IsRunning
                ? (s.CanStop ? Loc.T("workload.svc.runningStop") : Loc.T("workload.svc.running"))
                : (s.CanStart ? Loc.T("workload.svc.stoppedStart") : Loc.T("workload.svc.stopped"));
        return new SessionServiceRow
        {
            Name = s.Name,
            DisplayName = s.DisplayName,
            Title = s.DisplayName,
            Detail = s.Name + (s.IsBackgroundInterest ? " · " + Loc.T("workload.bg.tag") : ""),
            Status = LocalizeScmStatus(s.Status),
            Initial = initial,
            IsRunning = s.IsRunning,
            CanStop = s.CanStop,
            CanStart = s.CanStart,
            IsBlocked = s.IsBlocked,
            BlockLabel = action,
            StatusKind = statusKind
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v.Trim();
            }
        }

        return null;
    }

    private static string ShortenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "—";
        }

        var p = path.Trim();
        if (p.Length <= 64)
        {
            return p;
        }

        try
        {
            var file = System.IO.Path.GetFileName(p);
            var dir = System.IO.Path.GetDirectoryName(p) ?? "";
            var leaf = System.IO.Path.GetFileName(dir);
            if (!string.IsNullOrWhiteSpace(leaf) && !string.IsNullOrWhiteSpace(file))
            {
                return @"…\" + leaf + @"\" + file;
            }
        }
        catch
        {
            // fall through
        }

        return "…" + p[^48..];
    }

    private static string LocalizeScmStatus(string status) => status switch
    {
        "Running" => Loc.T("workload.svc.scm.running"),
        "Stopped" => Loc.T("workload.svc.scm.stopped"),
        "StartPending" => Loc.T("workload.svc.scm.startPending"),
        "StopPending" => Loc.T("workload.svc.scm.stopPending"),
        _ => status
    };

    private IEnumerable<SessionProcessRow> VisibleWorkloadProcessRows() =>
        WorkloadProcessList?.ItemsSource as IEnumerable<SessionProcessRow> ?? _workloadProcRows;

    private IEnumerable<SessionServiceRow> VisibleWorkloadServiceRows() =>
        WorkloadServiceList?.ItemsSource as IEnumerable<SessionServiceRow> ?? _workloadSvcRows;

    private void WorkloadProcessStop_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkloadProcessList is null || WorkloadServiceList is null)
        {
            return;
        }

        var procs = VisibleWorkloadProcessRows()
            .Where(r => r.IsSelected && r.CanSelect)
            .Select(r => r.Id)
            .ToArray();
        var svcs = VisibleWorkloadServiceRows()
            .Where(r => r.IsSelected && r.CanStop)
            .Select(r => r.Name)
            .ToArray();

        if (procs.Length == 0 && svcs.Length == 0)
        {
            if (WorkloadProcessStatus is not null)
            {
                WorkloadProcessStatus.Text = Loc.T("game.session.noneSelected");
            }

            return;
        }

        var confirm = MessageBox.Show(
            Loc.T("game.session.confirm")
                .Replace("{0}", procs.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{1}", svcs.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
            Loc.T("nav.workloads"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var result = SessionResourceController.StopSelected(procs, svcs);
        if (WorkloadProcessStatus is not null)
        {
            WorkloadProcessStatus.Text = Loc.T("game.session.result")
                .Replace("{0}", result.ProcessesStopped.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{1}", result.ServicesStopped.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{2}", result.BlockedSkipped.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                + "\n" + DescribeAvailableRam();
        }

        RefreshWorkloadProcessListAsync();
        RenderWorkloads();
    }

    private void WorkloadProcessStart_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkloadServiceList is null)
        {
            return;
        }

        var svcs = VisibleWorkloadServiceRows()
            .Where(r => r.IsSelected && r.CanStart)
            .Select(r => r.Name)
            .ToArray();
        if (svcs.Length == 0)
        {
            if (WorkloadProcessStatus is not null)
            {
                WorkloadProcessStatus.Text = Loc.T("workload.svc.noneToStart");
            }

            return;
        }

        var confirm = MessageBox.Show(
            Loc.T("workload.svc.startConfirm")
                .Replace("{0}", svcs.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
            Loc.T("nav.workloads"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var result = SessionResourceController.StartSelectedServices(svcs);
        if (WorkloadProcessStatus is not null)
        {
            WorkloadProcessStatus.Text = Loc.T("workload.svc.startResult")
                .Replace("{0}", result.ServicesStarted.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{1}", result.BlockedSkipped.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        RefreshWorkloadProcessListAsync();
        RenderWorkloads();
    }

    private void WorkloadMemReduct_OnClick(object sender, RoutedEventArgs e)
    {
        var entry = ToolHubService.LoadEntries()
            .FirstOrDefault(x => string.Equals(x.Id, MemReductHubId, StringComparison.OrdinalIgnoreCase));
        var apps = _session?.Applications ?? Array.Empty<ApplicationDefinition>();
        var facts = InstalledAppDetector.Scan();
        var present = entry is not null && ToolHubService.IsPresent(entry, apps, facts);

        if (present && entry is not null && ToolHubService.TryLaunch(entry))
        {
            WorkloadProcessStatus.Text = Loc.T("game.ram.opened") + "\n" + DescribeAvailableRam();
            return;
        }

        var app = apps.FirstOrDefault(a =>
            string.Equals(a.Id, MemReductAppId, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            MessageBox.Show(Loc.T("game.ram.missingCatalog"), Loc.T("nav.workloads"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            Loc.T("game.ram.installConfirm")
                .Replace("{0}", app.Title, StringComparison.Ordinal)
                .Replace("{1}", app.WingetId, StringComparison.Ordinal),
            Loc.T("nav.workloads"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        WorkloadProcessStatus.Text = Loc.T("game.ram.installing");
        BtnWorkloadMemReduct.IsEnabled = false;
        Task.Run(() => WingetClient.Install(app, allowElevate: true)).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                BtnWorkloadMemReduct.IsEnabled = true;
                if (t.IsFaulted)
                {
                    WorkloadProcessStatus.Text = Truncate(t.Exception?.GetBaseException().Message ?? "error", 400);
                    return;
                }

                var result = t.Result;
                WorkloadProcessStatus.Text = $"{result.Outcome}: {Truncate(result.Message, 240)}";
                if (result.Outcome is WingetOutcome.Ok or WingetOutcome.AlreadyInstalled)
                {
                    entry = ToolHubService.LoadEntries()
                        .FirstOrDefault(x => string.Equals(x.Id, MemReductHubId, StringComparison.OrdinalIgnoreCase));
                    if (entry is not null && ToolHubService.TryLaunch(entry))
                    {
                        WorkloadProcessStatus.Text = Loc.T("game.ram.opened") + "\n" + DescribeAvailableRam();
                    }
                }
            });
        }, TaskScheduler.Default);
    }
}

using System.Windows;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class MainWindow
{
    private GpuCacheScanResult? _gpuCacheScan;
    private bool _gpuCacheBusy;

    private IReadOnlyList<GpuInventory>? CurrentGpus() =>
        _session?.Inventory.Gpus;

    private void ApplyGpuCacheScan(GpuCacheScanResult scan)
    {
        _gpuCacheScan = scan;
        if (GpuCacheAdapters is null || GpuCacheStatus is null)
        {
            return;
        }

        var gaming = scan.Adapters.Where(a => a.IsLikelyGaming).ToArray();
        if (gaming.Length == 0)
        {
            GpuCacheAdapters.Text = Loc.T("gpu.cache.noGpu");
        }
        else
        {
            GpuCacheAdapters.Text = Loc.T("gpu.cache.adapters")
                .Replace("{0}", string.Join(" · ", gaming.Select(FormatAdapter)), StringComparison.Ordinal);
        }

        if (scan.Targets.Count == 0)
        {
            GpuCacheStatus.Text = Loc.T("gpu.cache.empty");
            return;
        }

        var lines = scan.Targets
            .Take(8)
            .Select(t => $"• {t.Title}: {GpuCacheCleaner.FormatBytes(t.Bytes)}");
        GpuCacheStatus.Text = Loc.T("gpu.cache.summary")
            .Replace("{0}", scan.Targets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{1}", GpuCacheCleaner.FormatBytes(scan.TotalBytes), StringComparison.Ordinal)
            + "\n" + string.Join("\n", lines);
    }

    private static string FormatAdapter(GpuAdapterHint a)
    {
        if (a.DedicatedBytes is > 0)
        {
            var gb = a.DedicatedBytes.Value / (1024d * 1024 * 1024);
            return $"{a.Name} [{a.Vendor}, {gb:0.0} GB]";
        }

        return $"{a.Name} [{a.Vendor}]";
    }

    private void RefreshGpuCacheCard(bool forceScan = false)
    {
        if (GpuCacheAdapters is null || GpuCacheStatus is null || _gpuCacheBusy)
        {
            return;
        }

        if (!forceScan && _gpuCacheScan is not null)
        {
            ApplyGpuCacheScan(_gpuCacheScan);
            return;
        }

        StartGpuCacheScan();
    }

    private void StartGpuCacheScan()
    {
        if (_gpuCacheBusy)
        {
            return;
        }

        _gpuCacheBusy = true;
        SetGpuCacheButtonsEnabled(false);
        if (GpuCacheStatus is not null)
        {
            GpuCacheStatus.Text = Loc.T("gpu.cache.scanning");
        }

        var gpus = CurrentGpus();
        _ = Task.Run(() => GpuCacheCleaner.Scan(gpus)).ContinueWith(t =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _gpuCacheBusy = false;
                SetGpuCacheButtonsEnabled(true);
                if (t.IsFaulted)
                {
                    if (GpuCacheStatus is not null)
                    {
                        GpuCacheStatus.Text = Truncate(t.Exception?.GetBaseException().Message ?? "error", 240);
                    }

                    return;
                }

                ApplyGpuCacheScan(t.Result);
            });
        }, TaskScheduler.Default);
    }

    private void SetGpuCacheButtonsEnabled(bool enabled)
    {
        if (BtnGpuCacheScan is not null)
        {
            BtnGpuCacheScan.IsEnabled = enabled;
        }

        if (BtnGpuCacheClean is not null)
        {
            BtnGpuCacheClean.IsEnabled = enabled;
        }
    }

    private void GpuCacheScan_OnClick(object sender, RoutedEventArgs e) => StartGpuCacheScan();

    private void GpuCacheClean_OnClick(object sender, RoutedEventArgs e) =>
        RunGpuCacheClean(status =>
        {
            if (GpuCacheStatus is not null)
            {
                GpuCacheStatus.Text = status;
            }
        });

    private void GameGpuCache_OnClick(object sender, RoutedEventArgs e)
    {
        SelectNav("workloads");
        RunGpuCacheClean(status =>
        {
            if (GpuCacheStatus is not null)
            {
                GpuCacheStatus.Text = status;
            }

            if (GameStatusText is not null)
            {
                GameStatusText.Text = status;
            }
        });
    }

    private void RunGpuCacheClean(Action<string> setStatus)
    {
        if (_gpuCacheBusy)
        {
            return;
        }

        _gpuCacheBusy = true;
        SetGpuCacheButtonsEnabled(false);
        setStatus(Loc.T("gpu.cache.scanning"));
        var gpus = CurrentGpus();
        _ = Task.Run(() => GpuCacheCleaner.Scan(gpus)).ContinueWith(t =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (t.IsFaulted)
                {
                    _gpuCacheBusy = false;
                    SetGpuCacheButtonsEnabled(true);
                    setStatus(Truncate(t.Exception?.GetBaseException().Message ?? "error", 240));
                    return;
                }

                var scan = t.Result;
                ApplyGpuCacheScan(scan);
                if (scan.Targets.Count == 0)
                {
                    _gpuCacheBusy = false;
                    SetGpuCacheButtonsEnabled(true);
                    setStatus(Loc.T("gpu.cache.empty"));
                    return;
                }

                var confirm = MessageBox.Show(
                    Loc.T("gpu.cache.confirm")
                        .Replace("{0}", scan.Targets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                        .Replace("{1}", GpuCacheCleaner.FormatBytes(scan.TotalBytes), StringComparison.Ordinal)
                        .Replace("{2}", string.Join(", ", scan.ActiveVendors), StringComparison.Ordinal),
                    Loc.T("gpu.cache.title"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    _gpuCacheBusy = false;
                    SetGpuCacheButtonsEnabled(true);
                    setStatus(Loc.T("gpu.cache.cancelled"));
                    return;
                }

                setStatus(Loc.T("gpu.cache.cleaning"));
                var targets = scan.Targets.ToArray();
                _ = Task.Run(() => GpuCacheCleaner.CleanTargets(targets)).ContinueWith(ct =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _gpuCacheBusy = false;
                        SetGpuCacheButtonsEnabled(true);
                        if (ct.IsFaulted)
                        {
                            setStatus(Truncate(ct.Exception?.GetBaseException().Message ?? "error", 240));
                            return;
                        }

                        var result = ct.Result;
                        setStatus(Loc.T("gpu.cache.result")
                            .Replace("{0}", GpuCacheCleaner.FormatBytes(result.BytesFreed), StringComparison.Ordinal)
                            .Replace("{1}", result.TargetsCleaned.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                            .Replace("{2}", result.FilesDeleted.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                            .Replace("{3}", result.Errors.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
                        _gpuCacheScan = null;
                        StartGpuCacheScan();
                    });
                }, TaskScheduler.Default);
            });
        }, TaskScheduler.Default);
    }
}

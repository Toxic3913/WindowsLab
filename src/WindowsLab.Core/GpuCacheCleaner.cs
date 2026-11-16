using System.Globalization;

namespace WindowsLab.Core;

public enum GpuCacheVendor
{
    Nvidia,
    Amd,
    Intel,
    DirectX
}

public sealed record GpuAdapterHint(
    string Name,
    string Vendor,
    long? DedicatedBytes,
    bool IsLikelyGaming);

public sealed record GpuCacheTarget(
    string Id,
    GpuCacheVendor Vendor,
    string Title,
    string Path,
    long Bytes,
    bool Exists);

public sealed record GpuCacheScanResult(
    IReadOnlyList<GpuAdapterHint> Adapters,
    IReadOnlyList<string> ActiveVendors,
    IReadOnlyList<GpuCacheTarget> Targets,
    long TotalBytes);

public sealed record GpuCacheCleanResult(
    int TargetsCleaned,
    long BytesFreed,
    int FilesDeleted,
    int Errors,
    IReadOnlyList<string> Messages);

/// <summary>
/// Detect gaming GPU vendors from inventory facts + present cache roots, then clean allowlisted
/// driver/shader caches only (D033). Never deletes whole vendor trees or protected OS paths.
/// </summary>
public static class GpuCacheCleaner
{
    public static IReadOnlyList<GpuAdapterHint> ResolveAdapters(IEnumerable<GpuInventory>? inventoryGpus)
    {
        var hints = new List<GpuAdapterHint>();
        if (inventoryGpus is not null)
        {
            foreach (var g in inventoryGpus)
            {
                if (string.IsNullOrWhiteSpace(g.Name))
                {
                    continue;
                }

                if (g.Name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)
                    || g.Vendor.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var vendor = NormalizeVendor(g.Vendor, g.Name);
                var gaming = IsLikelyGamingAdapter(vendor, g.DedicatedBytes, g.Name);
                hints.Add(new GpuAdapterHint(g.Name.Trim(), vendor, g.DedicatedBytes, gaming));
            }
        }

        // Folder presence when inventory empty — not marked as gaming by itself.
        foreach (var (vendor, marker) in FolderVendorMarkers())
        {
            if (hints.Any(h => h.Vendor.Equals(vendor, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (Directory.Exists(marker))
            {
                hints.Add(new GpuAdapterHint(vendor + " (cache folders)", vendor, null, false));
            }
        }

        return hints
            .OrderByDescending(h => h.IsLikelyGaming)
            .ThenByDescending(h => h.DedicatedBytes ?? 0)
            .ThenBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static GpuCacheScanResult Scan(IEnumerable<GpuInventory>? inventoryGpus = null)
    {
        var adapters = ResolveAdapters(inventoryGpus);
        var vendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in adapters.Where(a => a.IsLikelyGaming))
        {
            vendors.Add(a.Vendor);
        }

        // Include vendor caches that exist on disk even if inventory missed the adapter.
        foreach (var (vendor, marker) in FolderVendorMarkers())
        {
            if (Directory.Exists(marker))
            {
                vendors.Add(vendor);
            }
        }

        vendors.Add("DirectX");

        var targets = new List<GpuCacheTarget>();
        foreach (var def in Catalog())
        {
            if (!IsSafeAllowlistedRoot(def.Path))
            {
                continue;
            }

            var label = VendorLabel(def.Vendor);
            if (def.Vendor != GpuCacheVendor.DirectX && !vendors.Contains(label))
            {
                continue;
            }

            var bytes = MeasureDirectoryBytes(def.Path);
            if (bytes <= 0)
            {
                continue;
            }

            targets.Add(new GpuCacheTarget(def.Id, def.Vendor, def.Title, def.Path, bytes, true));
        }

        return new GpuCacheScanResult(
            adapters,
            vendors.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray(),
            targets.OrderByDescending(t => t.Bytes).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToArray(),
            targets.Sum(t => t.Bytes));
    }

    public static GpuCacheCleanResult Clean(IEnumerable<string> targetIds, IEnumerable<GpuInventory>? inventoryGpus = null)
    {
        var idSet = new HashSet<string>(targetIds, StringComparer.OrdinalIgnoreCase);
        if (idSet.Count == 0)
        {
            return new GpuCacheCleanResult(0, 0, 0, 0, ["nothing selected"]);
        }

        var scan = Scan(inventoryGpus);
        return CleanTargets(scan.Targets.Where(t => idSet.Contains(t.Id)).ToArray());
    }

    public static GpuCacheCleanResult CleanAllPresent(IEnumerable<GpuInventory>? inventoryGpus = null)
    {
        var scan = Scan(inventoryGpus);
        return CleanTargets(scan.Targets);
    }

    public static GpuCacheCleanResult CleanTargets(IReadOnlyList<GpuCacheTarget> targets)
    {
        var messages = new List<string>();
        var cleaned = 0;
        long freed = 0;
        var files = 0;
        var errors = 0;

        foreach (var target in targets)
        {
            if (!IsSafeAllowlistedRoot(target.Path) || !IsCatalogId(target.Id, target.Path))
            {
                errors++;
                messages.Add("blocked path: " + target.Path);
                continue;
            }

            var before = MeasureDirectoryBytes(target.Path);
            var (deleted, err) = ClearDirectoryContents(target.Path);
            files += deleted;
            errors += err;
            var after = Directory.Exists(target.Path) ? MeasureDirectoryBytes(target.Path) : 0;
            var delta = Math.Max(0, before - after);
            freed += delta;
            cleaned++;
            messages.Add($"{target.Id}: freed {FormatBytes(delta)} ({deleted} files, {err} errors)");
        }

        return new GpuCacheCleanResult(cleaned, freed, files, errors, messages);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return kb.ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        }

        var mb = kb / 1024d;
        if (mb < 1024)
        {
            return mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        return (mb / 1024d).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
    }

    private static bool IsLikelyGamingAdapter(string vendor, long? dedicatedBytes, string name)
    {
        if (vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (vendor.Equals("AMD", StringComparison.OrdinalIgnoreCase))
        {
            // Exclude weak iGPU-only labels when possible; dedicated VRAM or Radeon discrete names.
            if (dedicatedBytes is > 512L * 1024 * 1024)
            {
                return true;
            }

            return name.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("Radeon Pro", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("RX ", StringComparison.OrdinalIgnoreCase);
        }

        if (vendor.Equals("Intel", StringComparison.OrdinalIgnoreCase))
        {
            // Arc discrete / large dedicated; skip typical UHD/Iris shared iGPU as "gaming primary".
            if (name.Contains("Arc", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return dedicatedBytes is > 1024L * 1024 * 1024;
        }

        return false;
    }

    private static string NormalizeVendor(string vendor, string name)
    {
        if (!string.IsNullOrWhiteSpace(vendor)
            && !vendor.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            if (vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase)
                || vendor.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                return "NVIDIA";
            }

            if (vendor.Equals("AMD", StringComparison.OrdinalIgnoreCase)
                || vendor.Equals("ATI Technologies Inc.", StringComparison.OrdinalIgnoreCase)
                || vendor.StartsWith("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase))
            {
                return "AMD";
            }

            if (vendor.Equals("Intel", StringComparison.OrdinalIgnoreCase)
                || vendor.StartsWith("Intel", StringComparison.OrdinalIgnoreCase))
            {
                return "Intel";
            }
        }

        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GeForce", StringComparison.OrdinalIgnoreCase)
            || name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GTX", StringComparison.OrdinalIgnoreCase))
        {
            return "NVIDIA";
        }

        if (name.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("AMD ", StringComparison.OrdinalIgnoreCase))
        {
            return "AMD";
        }

        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Arc", StringComparison.OrdinalIgnoreCase)
            || name.Contains("UHD", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Iris", StringComparison.OrdinalIgnoreCase))
        {
            return "Intel";
        }

        return "Unknown";
    }

    private static string VendorLabel(GpuCacheVendor v) => v switch
    {
        GpuCacheVendor.Nvidia => "NVIDIA",
        GpuCacheVendor.Amd => "AMD",
        GpuCacheVendor.Intel => "Intel",
        GpuCacheVendor.DirectX => "DirectX",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
    };

    private static IEnumerable<(string Vendor, string Marker)> FolderVendorMarkers()
    {
        foreach (var path in new[]
                 {
                     TryCombine(Environment.SpecialFolder.LocalApplicationData, "NVIDIA"),
                     TryCombine(Environment.SpecialFolder.ApplicationData, "NVIDIA"),
                     TryCombine(Environment.SpecialFolder.LocalApplicationData, "AMD"),
                     TryCombine(Environment.SpecialFolder.LocalApplicationData, "Intel")
                 })
        {
            if (path is null)
            {
                continue;
            }

            if (path.Contains(@"\NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                yield return ("NVIDIA", path);
            }
            else if (path.Contains(@"\AMD", StringComparison.OrdinalIgnoreCase))
            {
                yield return ("AMD", path);
            }
            else if (path.Contains(@"\Intel", StringComparison.OrdinalIgnoreCase))
            {
                yield return ("Intel", path);
            }
        }
    }

    private sealed record CacheDef(string Id, GpuCacheVendor Vendor, string Title, string Path);

    private static List<CacheDef> Catalog()
    {
        var list = new List<CacheDef>();
        void Add(string id, GpuCacheVendor vendor, string title, Environment.SpecialFolder folder, params string[] parts)
        {
            var path = TryCombine(folder, parts);
            if (path is not null)
            {
                list.Add(new CacheDef(id, vendor, title, path));
            }
        }

        Add("nvidia.dxcache", GpuCacheVendor.Nvidia, "NVIDIA DXCache", Environment.SpecialFolder.LocalApplicationData, "NVIDIA", "DXCache");
        Add("nvidia.glcache", GpuCacheVendor.Nvidia, "NVIDIA GLCache", Environment.SpecialFolder.LocalApplicationData, "NVIDIA", "GLCache");
        Add("nvidia.computecache.roaming", GpuCacheVendor.Nvidia, "NVIDIA ComputeCache", Environment.SpecialFolder.ApplicationData, "NVIDIA", "ComputeCache");
        Add("nvidia.computecache.local", GpuCacheVendor.Nvidia, "NVIDIA ComputeCache (Local)", Environment.SpecialFolder.LocalApplicationData, "NVIDIA", "ComputeCache");
        Add("nvidia.nvcache.local", GpuCacheVendor.Nvidia, "NVIDIA NV_Cache (Local)", Environment.SpecialFolder.LocalApplicationData, "NVIDIA Corporation", "NV_Cache");
        Add("nvidia.nvcache.programdata", GpuCacheVendor.Nvidia, "NVIDIA NV_Cache (ProgramData)", Environment.SpecialFolder.CommonApplicationData, "NVIDIA Corporation", "NV_Cache");

        Add("amd.dxcache", GpuCacheVendor.Amd, "AMD DxCache", Environment.SpecialFolder.LocalApplicationData, "AMD", "DxCache");
        Add("amd.glcache", GpuCacheVendor.Amd, "AMD GLCache", Environment.SpecialFolder.LocalApplicationData, "AMD", "GLCache");
        Add("amd.vkcache", GpuCacheVendor.Amd, "AMD VkCache", Environment.SpecialFolder.LocalApplicationData, "AMD", "VkCache");
        Add("amd.dx9", GpuCacheVendor.Amd, "AMD DX9Cache", Environment.SpecialFolder.LocalApplicationData, "AMD", "DX9Cache");

        Add("intel.shader", GpuCacheVendor.Intel, "Intel ShaderCache", Environment.SpecialFolder.LocalApplicationData, "Intel", "ShaderCache");
        Add("intel.dxcache", GpuCacheVendor.Intel, "Intel DXCache", Environment.SpecialFolder.LocalApplicationData, "Intel", "DXCache");

        Add("dx.d3dscache", GpuCacheVendor.DirectX, "DirectX D3DSCache", Environment.SpecialFolder.LocalApplicationData, "D3DSCache");
        return list;
    }

    private static string? TryCombine(Environment.SpecialFolder folder, params string[] parts)
    {
        var root = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsCatalogId(string id, string path)
    {
        foreach (var c in Catalog())
        {
            if (!string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(c.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsSafeAllowlistedRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        if (!Catalog().Any(c =>
            {
                try
                {
                    return string.Equals(
                        Path.GetFullPath(c.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }))
        {
            return false;
        }

        try
        {
            var attrs = File.GetAttributes(full);
            if ((attrs & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        return Directory.Exists(full);
    }

    private static long MeasureDirectoryBytes(string path)
    {
        long total = 0;
        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (var file in Directory.EnumerateFiles(path, "*", opts))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // locked / race
                }
            }
        }
        catch
        {
            // AccessDenied on root
        }

        return total;
    }

    private static (int Deleted, int Errors) ClearDirectoryContents(string path)
    {
        var deleted = 0;
        var errors = 0;
        if (!Directory.Exists(path))
        {
            return (0, 0);
        }

        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var file in Directory.EnumerateFiles(path, "*", opts))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    errors++;
                }
            }

            foreach (var dir in Directory.EnumerateDirectories(path, "*", opts)
                         .OrderByDescending(d => d.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, recursive: false);
                    }
                }
                catch
                {
                    errors++;
                }
            }
        }
        catch
        {
            errors++;
        }

        return (deleted, errors);
    }
}

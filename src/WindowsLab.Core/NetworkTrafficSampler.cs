using System.Diagnostics;
using System.Net.NetworkInformation;

namespace WindowsLab.Core;

public sealed record LiveNetworkAdapterSnapshot(
    string Id,
    string Name,
    string Description,
    string? Ipv4,
    bool IsUp,
    double BytesReceivedPerSec,
    double BytesSentPerSec,
    long BytesReceived,
    long BytesSent);

public sealed record LiveNetworkSnapshot(
    string SummaryLine,
    double TotalBytesReceivedPerSec,
    double TotalBytesSentPerSec,
    IReadOnlyList<LiveNetworkAdapterSnapshot> Adapters);

/// <summary>
/// Per-adapter RX/TX rates via Network Interface performance counters (D032).
/// First sample after process start may read ~0 until the next timer tick.
/// </summary>
public static class NetworkTrafficSampler
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (PerformanceCounter Recv, PerformanceCounter Sent)> Counters = new(StringComparer.OrdinalIgnoreCase);
    private static bool _probed;
    private static bool _available;

    public static LiveNetworkSnapshot Sample()
    {
        EnsureCounters();
        var adapters = new List<LiveNetworkAdapterSnapshot>();
        double totalRecv = 0;
        double totalSent = 0;

        NetworkInterface[] nics;
        try
        {
            nics = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch
        {
            return new LiveNetworkSnapshot("", 0, 0, []);
        }

        foreach (var nic in nics)
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            string? ipv4 = null;
            try
            {
                ipv4 = nic.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address.ToString();
            }
            catch
            {
                // ignore
            }

            double recvRate = 0;
            double sentRate = 0;
            long recv = 0;
            long sent = 0;
            try
            {
                var stats = nic.GetIPStatistics();
                recv = stats.BytesReceived;
                sent = stats.BytesSent;
            }
            catch
            {
                // best-effort totals
            }

            if (_available)
            {
                var key = MatchCounterKey(nic.Description) ?? MatchCounterKey(nic.Name);
                if (key is not null && Counters.TryGetValue(key, out var pair))
                {
                    try
                    {
                        recvRate = Math.Max(0, pair.Recv.NextValue());
                        sentRate = Math.Max(0, pair.Sent.NextValue());
                    }
                    catch
                    {
                        recvRate = 0;
                        sentRate = 0;
                    }
                }
            }

            if (nic.OperationalStatus == OperationalStatus.Up)
            {
                totalRecv += recvRate;
                totalSent += sentRate;
            }

            adapters.Add(new LiveNetworkAdapterSnapshot(
                nic.Id,
                nic.Name,
                nic.Description,
                ipv4,
                nic.OperationalStatus == OperationalStatus.Up,
                recvRate,
                sentRate,
                recv,
                sent));
        }

        adapters.Sort((a, b) =>
        {
            var up = b.IsUp.CompareTo(a.IsUp);
            if (up != 0)
            {
                return up;
            }

            var traffic = (b.BytesReceivedPerSec + b.BytesSentPerSec).CompareTo(a.BytesReceivedPerSec + a.BytesSentPerSec);
            return traffic != 0
                ? traffic
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        var ips = adapters
            .Where(a => a.IsUp && !string.IsNullOrWhiteSpace(a.Ipv4))
            .Select(a => a.Ipv4!)
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        var summary = ips.Length == 0
            ? FormatRate(totalRecv) + " ↓ / " + FormatRate(totalSent) + " ↑"
            : string.Join(", ", ips) + " · " + FormatRate(totalRecv) + " ↓ / " + FormatRate(totalSent) + " ↑";

        return new LiveNetworkSnapshot(summary, totalRecv, totalSent, adapters);
    }

    public static string FormatRate(double bytesPerSec)
    {
        if (bytesPerSec < 1024)
        {
            return $"{bytesPerSec:0} B/s";
        }

        var kb = bytesPerSec / 1024d;
        if (kb < 1024)
        {
            return $"{kb:0.0} KB/s";
        }

        var mb = kb / 1024d;
        if (mb < 1024)
        {
            return $"{mb:0.00} MB/s";
        }

        return $"{mb / 1024d:0.00} GB/s";
    }

    private static string? MatchCounterKey(string? nicLabel)
    {
        if (string.IsNullOrWhiteSpace(nicLabel) || Counters.Count == 0)
        {
            return null;
        }

        var normalized = NormalizeInstance(nicLabel);
        foreach (var key in Counters.Keys)
        {
            if (string.Equals(NormalizeInstance(key), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        // Partial match: counter instance often truncates or replaces chars.
        foreach (var key in Counters.Keys)
        {
            var nk = NormalizeInstance(key);
            if (nk.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(nk, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    private static string NormalizeInstance(string s) =>
        s.Replace('(', '[').Replace(')', ']').Replace('#', '_').Trim();

    private static void EnsureCounters()
    {
        if (_probed)
        {
            return;
        }

        lock (Gate)
        {
            if (_probed)
            {
                return;
            }

            try
            {
                var cat = new PerformanceCounterCategory("Network Interface");
                foreach (var instance in cat.GetInstanceNames())
                {
                    if (string.IsNullOrWhiteSpace(instance)
                        || instance.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
                        || instance.Contains("isatap", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    PerformanceCounter? recv = null;
                    PerformanceCounter? sent = null;
                    try
                    {
                        recv = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance, true);
                        sent = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance, true);
                        _ = recv.NextValue();
                        _ = sent.NextValue();
                        Counters[instance] = (recv, sent);
                        recv = null;
                        sent = null;
                    }
                    catch
                    {
                        try { recv?.Dispose(); } catch { /* ignore */ }
                        try { sent?.Dispose(); } catch { /* ignore */ }
                    }
                }

                _available = Counters.Count > 0;
            }
            catch
            {
                _available = false;
            }

            _probed = true;
        }
    }
}

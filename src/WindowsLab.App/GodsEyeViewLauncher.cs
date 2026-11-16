using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace WindowsLab.App;

/// <summary>
/// Launch helper for bilawalsidhu/gods-eye-view (MIT app code; third-party data has its own terms).
/// Prefers a running local server, then Pinokio / official download docs.
/// </summary>
public static class GodsEyeViewLauncher
{
    public const string HubId = "hub.explore.gods-eye-view";
    public const string LocalUrl = "http://127.0.0.1:4173/";
    public const string DownloadUrl = "https://godseyeview.ai/download/";
    public const string PinokioAppUrl = "https://pinokio.co/apps/github-com-bilawalsidhu-gods-eye-view";
    public const string RepoUrl = "https://github.com/bilawalsidhu/gods-eye-view";
    public const string GuideUrl = "https://godseyeview.ai/guide/";
    public const string McpDocsUrl = "https://github.com/bilawalsidhu/gods-eye-view/blob/main/docs/MCP_SETUP.md";

    public enum OpenTarget
    {
        LocalServer,
        Pinokio,
        DownloadPage,
        Failed
    }

    public static bool IsLocalServerReachable(int timeoutMs = 400)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync("127.0.0.1", 4173);
            if (!task.Wait(timeoutMs))
            {
                return false;
            }

            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPinokioPresent()
    {
        foreach (var path in ExpandPinokioCandidates())
        {
            if (File.Exists(path))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsReady() => IsLocalServerReachable() || IsPinokioPresent();

    public static OpenTarget TryOpenBest()
    {
        if (IsLocalServerReachable() && TryOpenUrl(LocalUrl))
        {
            return OpenTarget.LocalServer;
        }

        if (IsPinokioPresent())
        {
            // Prefer Pinokio app page so Install/Start is one click; also try launching Pinokio.exe.
            _ = TryLaunchPinokio();
            if (TryOpenUrl(PinokioAppUrl))
            {
                return OpenTarget.Pinokio;
            }
        }

        return TryOpenUrl(DownloadUrl) ? OpenTarget.DownloadPage : OpenTarget.Failed;
    }

    public static bool TryOpenDocs() => TryOpenUrl(GuideUrl) || TryOpenUrl(RepoUrl);

    public static bool TryOpenMcpDocs() => TryOpenUrl(McpDocsUrl);

    private static bool TryLaunchPinokio()
    {
        foreach (var path in ExpandPinokioCandidates())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                return true;
            }
            catch
            {
                // try next
            }
        }

        return false;
    }

    private static IEnumerable<string> ExpandPinokioCandidates()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(local, "Programs", "Pinokio", "Pinokio.exe");
        yield return Path.Combine(local, "Pinokio", "Pinokio.exe");
        yield return Path.Combine(pf, "Pinokio", "Pinokio.exe");
        yield return Path.Combine(home, "pinokio", "pinokio.exe");
        yield return "Pinokio.exe";
    }

    private static bool TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}

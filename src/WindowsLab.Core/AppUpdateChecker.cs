using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowsLab.Core;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NoReleasePublished,
    NetworkError,
    Error
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string CurrentVersion,
    string? LatestTag,
    string? LatestName,
    string? ReleaseUrl,
    string? SetupAssetUrl,
    string Message);

/// <summary>
/// Checks GitHub Releases for Toxic3913/WindowsLab (public API, no token).
/// Does not auto-install — opens the release page for the operator to download.
/// </summary>
public static partial class AppUpdateChecker
{
    public const string GitHubOwner = "Toxic3913";
    public const string GitHubRepo = "WindowsLab";
    public const string ReleasesPageUrl = "https://github.com/Toxic3913/WindowsLab/releases";
    public const string LatestApiUrl = "https://api.github.com/repos/Toxic3913/WindowsLab/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    public static string GetCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly();
        var name = asm?.GetName().Name ?? "";
        if (asm is null || !name.StartsWith("WindowsLab", StringComparison.OrdinalIgnoreCase))
        {
            asm = typeof(AppUpdateChecker).Assembly;
        }

        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? info[..plus] : info;
        }

        var v = asm.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestApiUrl);
            using var resp = await Http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.NoReleasePublished,
                    current,
                    null,
                    null,
                    ReleasesPageUrl,
                    null,
                    "No hay release publicada en GitHub todavía. Puedes abrir la página de releases o usar el instalador en artifacts\\.");
            }

            if (!resp.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.NetworkError,
                    current,
                    null,
                    null,
                    ReleasesPageUrl,
                    null,
                    $"GitHub API {(int)resp.StatusCode}. Abre {ReleasesPageUrl}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var html = root.TryGetProperty("html_url", out var h) ? h.GetString() : ReleasesPageUrl;
            string? setupUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var an = asset.TryGetProperty("name", out var anEl) ? anEl.GetString() : null;
                    var url = asset.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : null;
                    if (an is null || url is null)
                    {
                        continue;
                    }

                    if (an.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                        && an.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        setupUrl = url;
                        break;
                    }
                }
            }

            var latestNorm = NormalizeVersion(tag);
            var currentNorm = NormalizeVersion(current);
            if (latestNorm is null || currentNorm is null)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.Error,
                    current,
                    tag,
                    name,
                    html,
                    setupUrl,
                    $"No se pudo comparar versiones (actual={current}, tag={tag}).");
            }

            var cmp = currentNorm.CompareTo(latestNorm);
            if (cmp >= 0)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.UpToDate,
                    current,
                    tag,
                    name,
                    html,
                    setupUrl,
                    $"Estás al día ({current}). Última release: {tag}.");
            }

            return new UpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                current,
                tag,
                name,
                html,
                setupUrl,
                $"Hay una actualización: {tag} (tienes {current}).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.NetworkError,
                current,
                null,
                null,
                ReleasesPageUrl,
                null,
                "Sin red o GitHub no responde: " + ex.Message);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Error,
                current,
                null,
                null,
                ReleasesPageUrl,
                null,
                ex.Message);
        }
    }

    /// <summary>True if remote is newer than local.</summary>
    public static bool IsNewer(string? remoteTag, string localVersion)
    {
        var a = NormalizeVersion(remoteTag);
        var b = NormalizeVersion(localVersion);
        return a is not null && b is not null && a.CompareTo(b) > 0;
    }

    internal static Version? NormalizeVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
        {
            s = s[1..];
        }

        // strip pre-release suffix: 0.3.0-beta → 0.3.0
        var dash = s.IndexOf('-', StringComparison.Ordinal);
        if (dash > 0)
        {
            s = s[..dash];
        }

        var m = VersionRegex().Match(s);
        if (!m.Success)
        {
            return null;
        }

        var major = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var minor = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        var build = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        return new Version(major, minor, build);
    }

    [GeneratedRegex(@"^(\d+)(?:\.(\d+))?(?:\.(\d+))?")]
    private static partial Regex VersionRegex();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsLab-UpdateChecker");
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }
}

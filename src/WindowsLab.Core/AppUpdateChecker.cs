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
    string? PortableZipUrl,
    string Message);

/// <summary>
/// Checks GitHub Releases for Toxic3913/WindowsLab.
/// Private repos need a PAT (Contents:read) in operator settings or GH_TOKEN / GITHUB_TOKEN.
/// </summary>
public static partial class AppUpdateChecker
{
    public const string GitHubOwner = "Toxic3913";
    public const string GitHubRepo = "WindowsLab";
    public const string ReleasesPageUrl = "https://github.com/Toxic3913/WindowsLab/releases";
    public const string LatestApiUrl = "https://api.github.com/repos/Toxic3913/WindowsLab/releases/latest";
    public const string ListApiUrl = "https://api.github.com/repos/Toxic3913/WindowsLab/releases?per_page=5";

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

    public static string? ResolveToken(string? explicitToken = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitToken))
        {
            return explicitToken.Trim();
        }

        try
        {
            var fromSettings = OperatorSettingsStore.Load().GitHubToken;
            if (!string.IsNullOrWhiteSpace(fromSettings))
            {
                return fromSettings.Trim();
            }
        }
        catch
        {
            // ignore
        }

        var env = Environment.GetEnvironmentVariable("WINDOWSLAB_GITHUB_TOKEN")
                  ?? Environment.GetEnvironmentVariable("GH_TOKEN")
                  ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    public static async Task<UpdateCheckResult> CheckAsync(
        string? githubToken = null,
        CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();
        var token = ResolveToken(githubToken);
        try
        {
            using var doc = await FetchLatestReleaseDocAsync(token, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.NoReleasePublished,
                    current,
                    null,
                    null,
                    ReleasesPageUrl,
                    null,
                    null,
                    token is null
                        ? "No se pudo leer releases (¿repo privado?). En Más pega un GitHub token (Contents:read) o haz el repo público. También puedes abrir la página de releases."
                        : "No hay release publicada o el token no tiene acceso a Releases.");
            }

            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var html = root.TryGetProperty("html_url", out var h) ? h.GetString() : ReleasesPageUrl;
            var (setupUrl, zipUrl) = PickAssets(root);

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
                    zipUrl,
                    $"No se pudo comparar versiones (actual={current}, tag={tag}).");
            }

            if (currentNorm.CompareTo(latestNorm) >= 0)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.UpToDate,
                    current,
                    tag,
                    name,
                    html,
                    setupUrl,
                    zipUrl,
                    $"Estás al día ({current}). Última release: {tag}.");
            }

            return new UpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                current,
                tag,
                name,
                html,
                setupUrl,
                zipUrl,
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
                null,
                ex.Message);
        }
    }

    public static async Task DownloadAssetAsync(
        string assetUrl,
        string destinationPath,
        string? githubToken = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, assetUrl);
        ApplyAuth(req, ResolveToken(githubToken));
        // GitHub asset redirects; ensure we accept octet-stream
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength;
        await using var input = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            82 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[82 * 1024];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readTotal += read;
            if (total is > 0)
            {
                progress?.Report(Math.Clamp(readTotal / (double)total.Value, 0, 1));
            }
        }

        progress?.Report(1);
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

    private static async Task<JsonDocument?> FetchLatestReleaseDocAsync(string? token, CancellationToken ct)
    {
        // Prefer /releases/latest
        using (var req = new HttpRequestMessage(HttpMethod.Get, LatestApiUrl))
        {
            ApplyAuth(req, token);
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            }

            if (resp.StatusCode is not System.Net.HttpStatusCode.NotFound
                and not System.Net.HttpStatusCode.Unauthorized
                and not System.Net.HttpStatusCode.Forbidden)
            {
                resp.EnsureSuccessStatusCode();
            }
        }

        // Fallback: first non-draft from list (helps some GitHub edge cases)
        using var listReq = new HttpRequestMessage(HttpMethod.Get, ListApiUrl);
        ApplyAuth(listReq, token);
        using var listResp = await Http.SendAsync(listReq, ct).ConfigureAwait(false);
        if (listResp.StatusCode is System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            return null;
        }

        listResp.EnsureSuccessStatusCode();
        await using var listStream = await listResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var listDoc = await JsonDocument.ParseAsync(listStream, cancellationToken: ct).ConfigureAwait(false);
        if (listDoc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var el in listDoc.RootElement.EnumerateArray())
        {
            var draft = el.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True;
            var pre = el.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True;
            if (draft || pre)
            {
                continue;
            }

            return JsonDocument.Parse(el.GetRawText());
        }

        return null;
    }

    private static (string? SetupUrl, string? ZipUrl) PickAssets(JsonElement root)
    {
        string? setupUrl = null;
        string? zipUrl = null;
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var an = asset.TryGetProperty("name", out var anEl) ? anEl.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : null;
            if (an is null || url is null)
            {
                continue;
            }

            if (setupUrl is null
                && an.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                && an.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                setupUrl = url;
            }

            if (zipUrl is null
                && an.Contains("portable", StringComparison.OrdinalIgnoreCase)
                && an.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = url;
            }
        }

        return (setupUrl, zipUrl);
    }

    private static void ApplyAuth(HttpRequestMessage req, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsLab-UpdateChecker/1.2");
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }
}

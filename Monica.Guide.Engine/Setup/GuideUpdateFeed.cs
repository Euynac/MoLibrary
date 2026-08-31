using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Monica.Guide.Setup;

/// <summary>One published GitHub release with the release assets the guide consumes.</summary>
public sealed record GuideUpdateInfo(
    string Tag,
    string Version,
    string Title,
    string Notes,
    DateTimeOffset? PublishedAt,
    Uri ArchiveUrl,
    long ArchiveSizeBytes,
    string ArchiveName,
    Uri ChecksumsUrl);

/// <summary>Outcome of one release-feed query against the installed product version.</summary>
public sealed record GuideUpdateCheck(
    bool UpdateAvailable,
    string InstalledVersion,
    GuideUpdateInfo? Latest,
    string? Error);

/// <summary>
/// Reads a product's public GitHub release feed and resolves the matching archive and
/// checksum assets. The repository slug and archive naming come from the product definition,
/// so one feed serves every product.
/// </summary>
public sealed class GuideUpdateFeed(string repositorySlug, string archiveAssetNameTemplate, HttpClient? httpClient = null, string? authorizationToken = null) : IDisposable
{
    // The GitHub API requires a descriptive user agent and is aggressive about idle connections;
    // downloads are performed by the stager with its own verified URL.
    private readonly HttpClient _http = httpClient ?? new HttpClient(
        new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private readonly bool _ownsHttp = httpClient is null;

    private Uri LatestReleaseApi => new($"https://api.github.com/repos/{repositorySlug}/releases/latest", UriKind.Absolute);

    /// <summary>Fetches the latest non-prerelease release and compares it with the installed version.</summary>
    public async Task<GuideUpdateCheck> CheckLatestAsync(
        string installedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installedVersion);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd("Monica-Guide");
            if (!string.IsNullOrWhiteSpace(authorizationToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", authorizationToken);
            }
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new GuideUpdateCheck(
                    false, installedVersion, null,
                    $"The release feed returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken);
            var latest = ResolveAssets(release);
            if (latest is null)
            {
                return new GuideUpdateCheck(
                    false, installedVersion, null,
                    $"Release '{release?.TagName ?? "unknown"}' has no archive and checksum assets.");
            }

            return new GuideUpdateCheck(
                CompareVersions(latest.Version, installedVersion) > 0,
                installedVersion,
                latest,
                null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or InvalidOperationException or System.Text.Json.JsonException)
        {
            return new GuideUpdateCheck(false, installedVersion, null, exception.Message);
        }
    }

    /// <summary>Downloads the checksum file of one release and returns the digest for the named asset.</summary>
    public static string? MatchChecksum(string checksumsText, string assetName)
    {
        foreach (var line in checksumsText.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            var separator = trimmed.IndexOf("  ", StringComparison.Ordinal);
            if (separator <= 0 || trimmed[(separator + 2)..].Trim() != assetName)
            {
                continue;
            }

            var digest = trimmed[..separator];
            return digest.Length == 64 && digest.All(char.IsAsciiHexDigitLower) ? digest : null;
        }

        return null;
    }

    internal GuideUpdateInfo? ResolveAssets(GitHubRelease? release)
    {
        if (release is null
            || string.IsNullOrWhiteSpace(release.TagName)
            || release.Assets is not { Count: > 0 } assets
            || !TryParseVersion(release.TagName, out var version))
        {
            return null;
        }

        var archiveName = archiveAssetNameTemplate
            .Replace("{version}", version.ToString(), StringComparison.Ordinal)
            .Replace("{rid}", AgentProductPlatform.CurrentRuntimeIdentifier, StringComparison.Ordinal);
        var archive = assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, archiveName, StringComparison.OrdinalIgnoreCase)
            && asset.Url is not null);
        var checksums = assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, "SHA256SUMS", StringComparison.OrdinalIgnoreCase)
            && asset.Url is not null);
        if (archive is null || checksums is null)
        {
            return null;
        }

        return new GuideUpdateInfo(
            release.TagName,
            version.ToString(),
            release.Name ?? release.TagName,
            release.Body ?? string.Empty,
            release.PublishedAt,
            // The API asset endpoints accept the local credential and redirect to a
            // signed URL; browser_download_url rejects credential-backed access.
            new Uri(archive.Url!, UriKind.Absolute),
            archive.Size,
            archive.Name!,
            new Uri(checksums.Url!, UriKind.Absolute));
    }

    /// <summary>Compares two product versions (with optional v prefix and prerelease suffix).</summary>
    public static int CompareVersions(string left, string right)
    {
        TryParseVersion(left, out var leftVersion);
        TryParseVersion(right, out var rightVersion);
        return leftVersion.CompareTo(rightVersion);
    }

    /// <summary>Parses a v-prefixed SemVer tag such as v0.2.0 or v0.2.0-preview.1.</summary>
    public static bool TryParseVersion(string text, out VersionInfo version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.StartsWith("v", StringComparison.Ordinal))
        {
            span = span[1..];
        }

        var prerelease = string.Empty;
        var separator = span.IndexOf('-');
        if (separator >= 0)
        {
            prerelease = span[separator..].ToString();
            span = span[..separator];
        }

        var segments = span.ToString().Split('.');
        if (segments.Length is < 1 or > 3
            || !segments.All(static segment => segment.Length > 0 && segment.All(char.IsDigit)))
        {
            return false;
        }

        version = new VersionInfo(
            int.Parse(segments[0]),
            segments.Length > 1 ? int.Parse(segments[1]) : 0,
            segments.Length > 2 ? int.Parse(segments[2]) : 0,
            prerelease);
        return true;
    }

    /// <summary>Parsed SemVer pieces where a prerelease sorts before its release.</summary>
    public readonly record struct VersionInfo(int Major, int Minor, int Patch, string Prerelease) : IComparable<VersionInfo>
    {
        public override string ToString()
            => $"{Major}.{Minor}.{Patch}{(Prerelease.Length == 0 ? string.Empty : Prerelease)}";

        public int CompareTo(VersionInfo other)
        {
            var numeric = Major.CompareTo(other.Major);
            if (numeric != 0) return numeric;
            numeric = Minor.CompareTo(other.Minor);
            if (numeric != 0) return numeric;
            numeric = Patch.CompareTo(other.Patch);
            if (numeric != 0) return numeric;

            var leftEmpty = Prerelease.Length == 0;
            var rightEmpty = other.Prerelease.Length == 0;
            if (leftEmpty || rightEmpty)
            {
                // No suffix is the final release and sorts above any prerelease.
                return leftEmpty && rightEmpty ? 0 : leftEmpty ? 1 : -1;
            }

            return ComparePrereleases(Prerelease, other.Prerelease);
        }

        /// <summary>Compares dot-separated prerelease identifiers per SemVer: numeric segments numerically and below alphanumeric ones.</summary>
        private static int ComparePrereleases(string left, string right)
        {
            var leftParts = left.Split('.');
            var rightParts = right.Split('.');
            var shared = Math.Min(leftParts.Length, rightParts.Length);
            for (var index = 0; index < shared; index++)
            {
                var leftNumeric = leftParts[index].All(char.IsAsciiDigit) && leftParts[index].Length > 0;
                var rightNumeric = rightParts[index].All(char.IsAsciiDigit) && rightParts[index].Length > 0;
                var order = leftNumeric && rightNumeric
                    ? long.Parse(leftParts[index]).CompareTo(long.Parse(rightParts[index]))
                    : leftNumeric ? -1
                    : rightNumeric ? 1
                    : string.CompareOrdinal(leftParts[index], rightParts[index]);
                if (order != 0)
                {
                    return order;
                }
            }

            // A longer identifier list with the same prefix is the newer prerelease.
            return leftParts.Length.CompareTo(rightParts.Length);
        }
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    /// <summary>GitHub release JSON shape limited to the fields the guide consumes.</summary>
    internal sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset>? Assets);

    internal sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("url")] string? Url);
}

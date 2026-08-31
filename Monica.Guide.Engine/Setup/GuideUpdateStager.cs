using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Monica.Guide.Setup;

/// <summary>Progress of one staged update, reported between download and validation phases.</summary>
public sealed record GuideUpdateProgress(string Phase, long Completed, long Total)
{
    public const string DownloadPhase = "download";
    public const string VerifyPhase = "verify";
    public const string ExtractPhase = "extract";
    public const string ValidatePhase = "validate";
}

/// <summary>A downloaded, checksum-verified, and fully validated release extracted beside the current install.</summary>
public sealed record GuideUpdateStageResult(
    bool Succeeded,
    string? BundleRoot,
    string? ProductVersion,
    string? Tag,
    string? Error);

/// <summary>
/// Downloads one published release, proves its bytes against the release checksums, and
/// extracts it as a complete validated bundle beside the currently installed bundle. The new
/// directory is never activated here; activation stays with the digest-locked guide configure
/// flow so updates inherit its transactions and recovery.
/// </summary>
public sealed class GuideUpdateStager(
    AgentProductDefinition? definition = null,
    HttpClient? httpClient = null,
    string? authorizationToken = null) : IDisposable
{
    // Release archives may be a hundred megabytes or more; a slow link may legitimately take
    // minutes, so only connect/send timeouts constrain the download.
    private readonly HttpClient _http = httpClient ?? new HttpClient(
        new SocketsHttpHandler
        {
            UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = DecompressionMethods.All
        })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
    private readonly bool _ownsHttp = httpClient is null;

    public async Task<GuideUpdateStageResult> StageAsync(
        GuideUpdateInfo update,
        string currentBundleRoot,
        IProgress<GuideUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentBundleRoot);
        try
        {
            var checksumDigest = await DownloadChecksumDigestAsync(update, cancellationToken);
            var archiveRootName = StripZipExtension(update.ArchiveName);
            var stagingRoot = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(currentBundleRoot))!,
                $".{archiveRootName}.staging-{Guid.NewGuid():N}");
            try
            {
                await DownloadArchiveAsync(update, checksumDigest, stagingRoot + ".zip", progress, cancellationToken);
                progress?.Report(new GuideUpdateProgress(GuideUpdateProgress.ExtractPhase, 0, 0));
                ZipFile.ExtractToDirectory(stagingRoot + ".zip", stagingRoot);
                RestoreExecutableBits(stagingRoot);
                progress?.Report(new GuideUpdateProgress(GuideUpdateProgress.ValidatePhase, 0, 0));

                // Candidate observation verifies the manifest, every file digest, and the
                // skill catalog before anything is allowed near the install.
                var release = ObserveCandidate(stagingRoot);
                var manifest = release.Manifest
                               ?? throw new InvalidDataException("The downloaded release has no release manifest.");
                if (!string.Equals(manifest.ProductVersion, update.Version, StringComparison.Ordinal)
                    || !string.Equals(manifest.Tag, update.Tag, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"The downloaded release advertises {manifest.Tag} ({manifest.ProductVersion}) "
                        + $"but the feed selected {update.Tag} ({update.Version}).");
                }

                var targetRoot = Path.Combine(
                    Path.GetDirectoryName(stagingRoot)!,
                    archiveRootName);
                if (Directory.Exists(targetRoot))
                {
                    // A previously staged copy of the exact same release is reusable
                    // after revalidation, so retrying a failed activation is idempotent.
                    try
                    {
                        var existing = ObserveCandidate(targetRoot);
                        if (existing.Manifest is not null
                            && string.Equals(existing.Manifest.ProductVersion, update.Version, StringComparison.Ordinal)
                            && string.Equals(existing.Manifest.Tag, update.Tag, StringComparison.Ordinal))
                        {
                            return new GuideUpdateStageResult(
                                true, targetRoot, existing.Manifest.ProductVersion, existing.Manifest.Tag, null);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
                    {
                        // Fall through: an unreadable leftover directory is reported below.
                    }

                    throw new InvalidOperationException(
                        $"The target directory already exists; move or remove it first: {targetRoot}");
                }

                Directory.Move(stagingRoot, targetRoot);
                return new GuideUpdateStageResult(true, targetRoot, manifest.ProductVersion, manifest.Tag, null);
            }
            finally
            {
                TryDelete(stagingRoot);
                TryDelete(stagingRoot + ".zip");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new GuideUpdateStageResult(false, null, null, null, exception.Message);
        }
    }

    private static string StripZipExtension(string archiveName)
        => archiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? archiveName[..^4]
            : throw new InvalidDataException($"Release archive '{archiveName}' is not a zip asset.");

    /// <summary>
    /// Zip extraction carries no faithful executable bit across every archiver, so the Unix
    /// entry binaries are made explicitly executable after extraction; Windows needs nothing.
    /// </summary>
    private void RestoreExecutableBits(string bundleRoot)
    {
        if (OperatingSystem.IsWindows() || definition is null)
        {
            return;
        }

        const UnixFileMode ExecutableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                           | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                           | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        foreach (var executableName in definition.Platforms.Select(static platform => platform.ExecutableName)
                     .Distinct(StringComparer.Ordinal))
        {
            foreach (var directory in (string[])[
                         Path.Combine(bundleRoot, "app"),
                         Path.Combine(bundleRoot, "setup")])
            {
                var candidate = Path.Combine(directory, executableName);
                if (File.Exists(candidate))
                {
                    File.SetUnixFileMode(candidate, ExecutableMode);
                }
            }
        }
    }

    private ReleaseObservation ObserveCandidate(string bundleRoot)
    {
        // When the product is known, its identity pins apply; otherwise the bundle's own
        // declared shape is validated in full.
        var appDirectory = Directory.Exists(Path.Combine(bundleRoot, "app"))
            ? Path.Combine(bundleRoot, "app")
            : bundleRoot;
        return GuideReleaseMetadata.Observe(appDirectory, null, definition);
    }

    private async Task<string> DownloadChecksumDigestAsync(GuideUpdateInfo update, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, update.ChecksumsUrl);
        request.Headers.UserAgent.ParseAdd("Monica-Guide");
        // The asset API requires the binary accept header before it redirects.
        request.Headers.Accept.ParseAdd("application/octet-stream");
        if (!string.IsNullOrWhiteSpace(authorizationToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", authorizationToken);
        }
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Downloading the release checksums failed with HTTP {(int)response.StatusCode}.");
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return GuideUpdateFeed.MatchChecksum(text, update.ArchiveName)
               ?? throw new InvalidDataException(
                   $"The release SHA256SUMS has no lowercase hex digest for '{update.ArchiveName}'.");
    }

    private async Task DownloadArchiveAsync(
        GuideUpdateInfo update,
        string expectedDigest,
        string archivePath,
        IProgress<GuideUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, update.ArchiveUrl);
        request.Headers.UserAgent.ParseAdd("Monica-Guide");
        // The asset API requires the binary accept header before it redirects.
        request.Headers.Accept.ParseAdd("application/octet-stream");
        if (!string.IsNullOrWhiteSpace(authorizationToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", authorizationToken);
        }
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Downloading the release archive failed with HTTP {(int)response.StatusCode}.");
        }

        var total = update.ArchiveSizeBytes > 0 ? update.ArchiveSizeBytes : response.Content.Headers.ContentLength ?? 0;
        long completed = 0;
        int read;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = File.Create(archivePath))
        {
            var buffer = new byte[81920];
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                completed += read;
                progress?.Report(new GuideUpdateProgress(GuideUpdateProgress.DownloadPhase, completed, total));
            }
        }

        progress?.Report(new GuideUpdateProgress(GuideUpdateProgress.VerifyPhase, completed, total));
        string actualDigest;
        await using (var archiveStream = File.OpenRead(archivePath))
        {
            actualDigest = Convert.ToHexString(
                    await SHA256.HashDataAsync(archiveStream, cancellationToken))
                .ToLowerInvariant();
        }

        if (!string.Equals(actualDigest, expectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The downloaded archive digest {actualDigest} does not match the published {expectedDigest}.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}

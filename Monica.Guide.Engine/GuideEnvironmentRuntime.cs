using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Monica.Guide;

internal interface IGuideEnvironmentRuntime
{
    IReadOnlyList<GuideEnvironment> DetectEnvironments();

    bool CommandExists(GuideEnvironment environment, string command);

    Task<GuideProcessResult> RunAsync(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);

    /// <summary>Runs one command with an explicit timeout bound instead of the default.</summary>
    Task<GuideProcessResult> RunAsync(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Observes a whole environment tree in one batch: redirection state and entry kind for
    /// the requested paths, content digests for the requested files, and recursive listings
    /// for the requested roots. Batching exists so skill-catalog verification costs a fixed
    /// number of host processes instead of one process per file.
    /// </summary>
    Task<GuideTreeObservation> ObserveTreeAsync(
        GuideEnvironment environment,
        string homeRoot,
        IReadOnlyList<string> observePaths,
        IReadOnlyList<string> digestFiles,
        IReadOnlyList<string> listRoots,
        CancellationToken cancellationToken);

    Task<string?> GetEnvironmentVariableAsync(
        GuideEnvironment environment,
        string name,
        CancellationToken cancellationToken);

    Task<byte[]?> ReadFileAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken);

    Task<GuideFileSystemEntryKind?> GetEntryKindAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuideFileSystemEntry>> ListEntriesAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken);

    Task<bool> HasPathRedirectionAsync(
        GuideEnvironment environment,
        string root,
        string path,
        CancellationToken cancellationToken);

    Task WriteFileAsync(
        GuideEnvironment environment,
        string path,
        byte[] content,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken);

    /// <summary>Deletes one directory that must be empty; a non-empty directory fails the call.</summary>
    Task DeleteDirectoryAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes many files in one environment with the fewest host processes possible. Every file
    /// lands through a temporary sibling and an atomic move, matching <see cref="WriteFileAsync" />.
    /// </summary>
    Task WriteFilesAsync(
        GuideEnvironment environment,
        IReadOnlyList<(string Path, byte[] Content)> files,
        CancellationToken cancellationToken);

    /// <summary>Creates one directory including parents; idempotent.</summary>
    Task CreateDirectoryAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken);

    /// <summary>Deletes one directory tree recursively; idempotent when the path is absent.</summary>
    Task DeleteTreeAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken);

    /// <summary>Moves one directory; the destination must not exist.</summary>
    Task MoveDirectoryAsync(
        GuideEnvironment environment,
        string fromPath,
        string toPath,
        CancellationToken cancellationToken);

    /// <summary>Best-effort first line of <c>&lt;command&gt; --version</c>; null when unavailable.</summary>
    Task<string?> CommandVersionAsync(
        GuideEnvironment environment,
        string command,
        CancellationToken cancellationToken);

    string UserHome(GuideEnvironment environment);
}

internal sealed record GuidePathObservation(bool Redirected, GuideFileSystemEntryKind? Kind);

internal sealed record GuideTreeObservation(
    IReadOnlyDictionary<string, GuidePathObservation> Paths,
    IReadOnlyDictionary<string, string> FileDigests,
    IReadOnlyDictionary<string, IReadOnlyList<GuideFileSystemEntry>> RootEntries)
{
    public static GuideTreeObservation Empty { get; } = new(
        new Dictionary<string, GuidePathObservation>(),
        new Dictionary<string, string>(),
        new Dictionary<string, IReadOnlyList<GuideFileSystemEntry>>());
}

internal sealed class GuideEnvironmentRuntime(IGuideHostEnvironment host)
    : IGuideEnvironmentRuntime
{
    // Claude 2.x `mcp get` live-probes the server and can take half a minute when nothing
    // listens, so host observation keeps a generous default bound.
    private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShortProcessTimeout = TimeSpan.FromSeconds(15);
    // Windows command lines are bounded near 32k characters; stay comfortably below it so
    // one batched observation can never exceed what wsl.exe can relay.
    private const int MaxScriptCharacters = 6_000;

    public IReadOnlyList<GuideEnvironment> DetectEnvironments()
    {
        var result = new List<GuideEnvironment>();
        if (host.Platform == GuideHostPlatform.Windows)
        {
            result.Add(new GuideEnvironment("windows", "windows"));
            foreach (var distro in RunLocal("wsl.exe", ["--list", "--quiet"]).StandardOutput
                         .Replace("\0", string.Empty, StringComparison.Ordinal)
                         .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                result.Add(new GuideEnvironment($"wsl:{distro}", "wsl", distro));
            }
        }
        else if (host.Platform == GuideHostPlatform.Wsl)
        {
            if (RunLocal("cmd.exe", ["/d", "/c", "ver"]).ExitCode == 0)
            {
                result.Add(new GuideEnvironment("windows", "windows"));
            }
            var distro = host.GetEnvironmentVariable("WSL_DISTRO_NAME") ?? "current";
            result.Add(new GuideEnvironment($"wsl:{distro}", "wsl", distro));
        }
        else
        {
            // Native Unix hosts keep their own identity; skills and ownership records key on it.
            var kind = host.Platform == GuideHostPlatform.MacOS ? "macos" : "linux";
            result.Add(new GuideEnvironment(kind, kind));
        }

        return result.DistinctBy(static value => value.Selector, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool CommandExists(GuideEnvironment environment, string command)
    {
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            return RunWsl(environment, "sh", ["-lc", $"command -v {ShellQuote(command)}"]).ExitCode == 0;
        }

        if (environment.Kind == "windows" && !OperatingSystem.IsWindows())
        {
            if (environment.Selector == "windows" && host.Platform == GuideHostPlatform.Wsl)
            {
                return RunLocal("cmd.exe", ["/d", "/c", "where", command]).ExitCode == 0;
            }
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return ResolveLocalWindowsCommand(command) is not null;
        }

        return RunLocal("sh", ["-lc", $"command -v {ShellQuote(command)}"]).ExitCode == 0;
    }

    public Task<GuideProcessResult> RunAsync(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
        => RunAsync(environment, executable, arguments, DefaultProcessTimeout, cancellationToken);

    public async Task<GuideProcessResult> RunAsync(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            return await RunWslAsync(environment, executable, arguments, timeout, cancellationToken);
        }
        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            return await RunLocalAsync(
                executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? executable : $"{executable}.exe",
                arguments,
                timeout,
                cancellationToken);
        }
        if (environment.Kind == "windows" && OperatingSystem.IsWindows())
        {
            return await RunLocalWindowsCommandAsync(executable, arguments, timeout, cancellationToken);
        }
        return await RunLocalAsync(executable, arguments, timeout, cancellationToken);
    }

    public async Task<GuideTreeObservation> ObserveTreeAsync(
        GuideEnvironment environment,
        string homeRoot,
        IReadOnlyList<string> observePaths,
        IReadOnlyList<string> digestFiles,
        IReadOnlyList<string> listRoots,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(homeRoot);
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            return await ObserveWslTreeAsync(environment, homeRoot, observePaths, digestFiles, listRoots, cancellationToken);
        }

        var comparer = environment.Kind == "windows"
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var paths = new Dictionary<string, GuidePathObservation>(comparer);
        var digests = new Dictionary<string, string>(comparer);
        var rootEntries = new Dictionary<string, IReadOnlyList<GuideFileSystemEntry>>(comparer);

        // When a WSL-hosted guide inspects the Windows environment, requested paths are
        // Windows paths and every probe must run against their /mnt equivalents instead.
        var inspectingWindowsFromWsl = environment.Kind == "windows"
                                       && host.Platform == GuideHostPlatform.Wsl;
        string Localize(string value)
            => inspectingWindowsFromWsl
               && GuideHostPath.TryConvertWindowsPathToWsl(value, out var local)
                ? local
                : value;

        // Every requested observation path shares the per-segment scan below home, so the
        // segments are visited once and each path's redirection resolves from the shared map.
        var segmentsByPath = new List<(string Path, IReadOnlyList<string> Segments)>();
        var segmentSet = new HashSet<string>(comparer) { Localize(homeRoot) };
        foreach (var path in observePaths.Distinct(comparer))
        {
            var segments = EnumerateContainedPath(environment, Localize(homeRoot), Localize(path));
            segmentsByPath.Add((path, segments));
            segmentSet.UnionWith(segments);
        }
        var segmentKinds = new Dictionary<string, GuideFileSystemEntryKind?>(comparer);
        foreach (var segment in segmentSet)
        {
            segmentKinds[segment] = LocalEntryKind(segment);
        }
        foreach (var (path, segments) in segmentsByPath)
        {
            // A missing segment hides everything below it, matching the sequential
            // stop-at-first-missing semantics of per-path redirection checks.
            var redirected = false;
            foreach (var segment in segments)
            {
                var kind = segmentKinds[segment];
                if (kind is null)
                {
                    break;
                }
                if (kind == GuideFileSystemEntryKind.Redirect)
                {
                    redirected = true;
                    break;
                }
            }
            paths[path] = new GuidePathObservation(redirected, LocalEntryKind(Localize(path)));
        }

        foreach (var file in digestFiles.Distinct(comparer))
        {
            var digest = await HashLocalFileAsync(Localize(file), cancellationToken);
            if (digest is not null)
            {
                digests[file] = digest;
            }
        }

        foreach (var root in listRoots.Distinct(comparer))
        {
            var entries = await ListLocalEntriesAsync(environment, Localize(root), cancellationToken);
            rootEntries[root] = entries;
        }

        return new GuideTreeObservation(paths, digests, rootEntries);
    }

    private async Task<GuideTreeObservation> ObserveWslTreeAsync(
        GuideEnvironment environment,
        string homeRoot,
        IReadOnlyList<string> observePaths,
        IReadOnlyList<string> digestFiles,
        IReadOnlyList<string> listRoots,
        CancellationToken cancellationToken)
    {
        var comparer = StringComparer.Ordinal;
        var observeList = observePaths.Distinct(comparer).ToArray();
        var fileList = digestFiles.Distinct(comparer).ToArray();
        var rootList = listRoots.Distinct(comparer).ToArray();

        // The script probes path segments, so the shared parent directories of a skill
        // catalog collapse into one probe regardless of how many files sit below them.
        var segmentsByPath = new List<(string Path, IReadOnlyList<string> Segments)>();
        var segmentSet = new HashSet<string>(comparer) { homeRoot };
        foreach (var path in observeList)
        {
            var segments = EnumerateContainedPath(environment, homeRoot, path);
            segmentsByPath.Add((path, segments));
            segmentSet.UnionWith(segments);
        }
        var probes = segmentSet.ToArray();

        var chunks = new List<(string[] Segments, string[] Files, string[] Roots)>();
        var segmentBatch = new List<string>();
        var fileBatch = new List<string>();
        var rootBatch = new List<string>();
        // Each item costs roughly its own quoted length; the shared function preamble is a
        // fixed overhead charged once per chunk. The bound stays far below the Windows
        // 32k command-line limit even for pathological paths.
        const int PreambleEstimate = 700;
        var estimate = PreambleEstimate;
        void Flush()
        {
            chunks.Add((segmentBatch.ToArray(), fileBatch.ToArray(), rootBatch.ToArray()));
            segmentBatch = [];
            fileBatch = [];
            rootBatch = [];
            estimate = PreambleEstimate;
        }
        foreach (var unit in probes.Select(value => (Item: value, Kind: 'S'))
                     .Concat(fileList.Select(value => (Item: value, Kind: 'F')))
                     .Concat(rootList.Select(value => (Item: value, Kind: 'R'))))
        {
            var target = unit.Kind switch
            {
                'S' => segmentBatch,
                'F' => fileBatch,
                _ => rootBatch
            };
            target.Add(unit.Item);
            estimate += unit.Item.Length + 10;
            if (estimate >= MaxScriptCharacters)
            {
                Flush();
            }
        }
        if (segmentBatch.Count > 0 || fileBatch.Count > 0 || rootBatch.Count > 0)
        {
            chunks.Add((segmentBatch.ToArray(), fileBatch.ToArray(), rootBatch.ToArray()));
        }

        var chunkResults = await Task.WhenAll(chunks.Select(chunk =>
            RunWslAsync(
                environment,
                "sh",
                ["-lc", BuildWslObserveScript(homeRoot, chunk.Segments, chunk.Files, chunk.Roots)],
                DefaultProcessTimeout,
                cancellationToken)));
        var segmentRecords = new Dictionary<string, (char Kind, string Device)>(comparer);
        var homeDevice = "?";
        var digests = new Dictionary<string, string>(comparer);
        var rootEntries = new Dictionary<string, IReadOnlyList<GuideFileSystemEntry>>(comparer);
        foreach (var chunkResult in chunkResults)
        {
            if (chunkResult.ExitCode != 0)
            {
                throw new IOException(
                    string.IsNullOrWhiteSpace(chunkResult.StandardError)
                        ? "The batched WSL tree observation failed."
                        : chunkResult.StandardError);
            }
            ParseWslObservation(
                chunkResult.StandardOutput,
                segmentRecords,
                ref homeDevice,
                digests,
                rootEntries);
        }

        var paths = new Dictionary<string, GuidePathObservation>(comparer);
        foreach (var (path, pathSegments) in segmentsByPath)
        {
            var redirected = false;
            foreach (var segment in pathSegments)
            {
                if (!segmentRecords.TryGetValue(segment, out var record) || record.Kind == 'm')
                {
                    break;
                }
                if (record.Kind == 'l' || (record.Device != "-" && record.Device != homeDevice))
                {
                    redirected = true;
                    break;
                }
            }
            var kind = segmentRecords.TryGetValue(path, out var pathRecord)
                ? ParseEntryKind(pathRecord.Kind)
                : null;
            paths[path] = new GuidePathObservation(redirected, kind);
        }

        return new GuideTreeObservation(paths, digests, rootEntries);
    }

    /// <summary>Builds the NUL-framed observation script executed once per chunk in WSL.</summary>
    internal static string BuildWslObserveScript(
        string homeRoot,
        IReadOnlyList<string> segments,
        IReadOnlyList<string> files,
        IReadOnlyList<string> roots)
    {
        var script = new StringBuilder();
        script.Append(
            "h=$(stat -c %m -- " + ShellQuote(homeRoot) + " 2>/dev/null) || h='-'; printf 'H\\0%s\\0' \"$h\"; " +
            "p(){ if [ -L \"$1\" ]; then printf 'S\\0%s\\0l\\0-\\0' \"$1\"; " +
            "elif [ -d \"$1\" ]; then printf 'S\\0%s\\0d\\0%s\\0' \"$1\" \"$(stat -c %m -- \"$1\" 2>/dev/null)\"; " +
            "elif [ -f \"$1\" ]; then printf 'S\\0%s\\0f\\0%s\\0' \"$1\" \"$(stat -c %m -- \"$1\" 2>/dev/null)\"; " +
            "elif [ -e \"$1\" ]; then printf 'S\\0%s\\0o\\0%s\\0' \"$1\" \"$(stat -c %m -- \"$1\" 2>/dev/null)\"; " +
            "else printf 'S\\0%s\\0m\\0-\\0' \"$1\"; fi; }; " +
            "f(){ if [ -f \"$1\" ]; then printf 'F\\0%s\\0%s\\0' \"$(sha256sum -- \"$1\" | cut -c 1-64)\" \"$1\"; " +
            "else printf 'M\\0%s\\0' \"$1\"; fi; }; " +
            "r(){ if [ -d \"$1\" ] && [ ! -L \"$1\" ]; then printf 'R\\0%s\\0' \"$1\"; " +
            "find \"$1\" -mindepth 1 -printf '%y\\0%p\\0'; fi; }; ");
        foreach (var segment in segments)
        {
            script.Append("p ").Append(ShellQuote(segment)).Append("; ");
        }
        foreach (var file in files)
        {
            script.Append("f ").Append(ShellQuote(file)).Append("; ");
        }
        foreach (var root in roots)
        {
            script.Append("r ").Append(ShellQuote(root)).Append("; ");
        }
        return script.ToString();
    }

    /// <summary>State-machine parser for the NUL-framed observation stream.</summary>
    internal static void ParseWslObservation(
        string output,
        Dictionary<string, (char Kind, string Device)> segments,
        ref string homeDevice,
        Dictionary<string, string> digests,
        Dictionary<string, IReadOnlyList<GuideFileSystemEntry>> rootEntries)
    {
        var tokens = output.Split('\0');
        var index = 0;
        while (index < tokens.Length)
        {
            var token = tokens[index];
            if (token.Length == 0 && index == tokens.Length - 1)
            {
                break;
            }

            switch (token)
            {
                case "H":
                    homeDevice = tokens[++index];
                    break;
                case "S":
                {
                    var path = tokens[++index];
                    var kind = tokens[++index];
                    var device = tokens[++index];
                    if (kind.Length != 1)
                    {
                        throw new InvalidDataException("WSL returned an invalid observation record.");
                    }
                    segments[path] = (kind[0], device);
                    break;
                }
                case "M":
                    // Missing digest file: consume the path token that follows the marker.
                    index++;
                    break;
                case "F":
                {
                    var digest = tokens[++index];
                    var path = tokens[++index];
                    digests[path] = digest;
                    break;
                }
                case "R":
                {
                    var root = tokens[++index];
                    var listing = new List<GuideFileSystemEntry>();
                    rootEntries[root] = listing;
                    index++;
                    while (index < tokens.Length
                           && !IsMarker(tokens[index])
                           && !(tokens[index].Length == 0 && index == tokens.Length - 1))
                    {
                        var kindToken = tokens[index];
                        if (kindToken.Length != 1 || index + 1 >= tokens.Length)
                        {
                            throw new InvalidDataException("WSL returned a malformed filesystem entry stream.");
                        }
                        listing.Add(new GuideFileSystemEntry(
                            tokens[index + 1],
                            ParseEntryKind(kindToken[0]) ?? GuideFileSystemEntryKind.Other));
                        index += 2;
                    }
                    // Step back onto the terminating token so the shared loop advance
                    // lands on it: either the next record marker or the stream end.
                    index--;
                    break;
                }
                default:
                    throw new InvalidDataException("WSL returned an unrecognized observation record.");
            }
            index++;
        }

        foreach (var root in rootEntries.Keys.ToArray())
        {
            rootEntries[root] = rootEntries[root]
                .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private static bool IsMarker(string token)
        => token is "H" or "S" or "M" or "F" or "R";

    private string? ResolveLocalWindowsCommand(string command)
        => GuideWindowsCommand.Resolve(
            command,
            value => RunLocal("where.exe", [value]),
            host.GetEnvironmentVariable("PATH"),
            host.GetEnvironmentVariable("PATHEXT"),
            File.Exists);

    private static string ShellQuote(string value) => $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    public async Task<string?> GetEnvironmentVariableAsync(
        GuideEnvironment environment,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (name.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("Environment variable names must use ASCII letters, digits, or underscore.", nameof(name));
        }

        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = await RunAsync(environment, "printenv", [name], ShortProcessTimeout, cancellationToken);
            return result.ExitCode == 0
                ? result.StandardOutput.TrimEnd('\r', '\n')
                : null;
        }
        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            var result = await RunLocalAsync("cmd.exe", ["/d", "/c", "set", name], DefaultProcessTimeout, cancellationToken);
            var prefix = name + "=";
            var value = result.ExitCode == 0
                ? result.StandardOutput
                    .Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                : null;
            return value is null ? null : value[prefix.Length..];
        }
        return host.GetEnvironmentVariable(name);
    }

    public async Task<byte[]?> ReadFileAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = await RunWslAsync(environment, "base64", [path], DefaultProcessTimeout, cancellationToken);
            return result.ExitCode == 0
                ? Convert.FromBase64String(result.StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal))
                : null;
        }

        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(path, out var wslPath))
            {
                return null;
            }
            return File.Exists(wslPath) ? await File.ReadAllBytesAsync(wslPath, cancellationToken) : null;
        }

        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task<GuideFileSystemEntryKind?> GetEntryKindAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var script =
                $"if [ ! -e {ShellQuote(path)} ] && [ ! -L {ShellQuote(path)} ]; then exit 0; fi; " +
                $"find {ShellQuote(path)} -maxdepth 0 -printf '%y'";
            var result = RunWsl(environment, "sh", ["-lc", script]);
            if (result.ExitCode != 0) throw new IOException(result.StandardError);
            return Task.FromResult(ParseEntryKind(result.StandardOutput.FirstOrDefault()));
        }

        var localPath = path;
        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(path, out localPath))
            {
                return Task.FromResult<GuideFileSystemEntryKind?>(null);
            }
        }
        return Task.FromResult(LocalEntryKind(localPath));
    }

    public Task<IReadOnlyList<GuideFileSystemEntry>> ListEntriesAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var script =
                $"if [ ! -e {ShellQuote(path)} ] && [ ! -L {ShellQuote(path)} ]; then exit 0; fi; " +
                $"find {ShellQuote(path)} -mindepth 1 -printf '%y\\0%p\\0'";
            var result = RunWsl(environment, "sh", ["-lc", script]);
            if (result.ExitCode != 0)
            {
                throw new IOException(result.StandardError);
            }
            var fields = result.StandardOutput.Split('\0');
            if (fields.Length % 2 != 1 || fields[^1].Length != 0)
            {
                throw new InvalidDataException("WSL returned a malformed filesystem entry stream.");
            }
            var wslEntries = new List<GuideFileSystemEntry>();
            for (var index = 0; index < fields.Length - 1; index += 2)
            {
                if (fields[index].Length != 1)
                {
                    throw new InvalidDataException("WSL returned an invalid filesystem entry kind.");
                }
                wslEntries.Add(new GuideFileSystemEntry(
                    fields[index + 1],
                    ParseEntryKind(fields[index][0]) ?? GuideFileSystemEntryKind.Other));
            }
            return Task.FromResult<IReadOnlyList<GuideFileSystemEntry>>(wslEntries
                .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
                .ToArray());
        }

        var localPath = path;
        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl
            && !GuideHostPath.TryConvertWindowsPathToWsl(path, out localPath))
        {
            return Task.FromResult<IReadOnlyList<GuideFileSystemEntry>>([]);
        }
        return ListLocalEntriesAsync(environment, localPath, CancellationToken.None);
    }

    public Task<bool> HasPathRedirectionAsync(
        GuideEnvironment environment,
        string root,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var candidate in EnumerateContainedPath(environment, root, path))
        {
            if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
            {
                var script =
                    $"if [ -e {ShellQuote(candidate)} ] || [ -L {ShellQuote(candidate)} ]; then " +
                    $"test -L {ShellQuote(candidate)} || " +
                    $"[ \"$(stat -c %m -- {ShellQuote(candidate)})\" != \"$(stat -c %m -- {ShellQuote(root)})\" ]; " +
                    "else exit 1; fi";
                if (RunWsl(environment, "sh", ["-lc", script]).ExitCode == 0)
                {
                    return Task.FromResult(true);
                }
                continue;
            }

            var localCandidate = candidate;
            if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
            {
                if (!GuideHostPath.TryConvertWindowsPathToWsl(candidate, out localCandidate))
                {
                    throw new InvalidOperationException($"Cannot map Windows path into WSL: {candidate}");
                }
            }

            try
            {
                if ((File.GetAttributes(localCandidate) & FileAttributes.ReparsePoint) != 0)
                {
                    return Task.FromResult(true);
                }
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
            }
        }

        return Task.FromResult(false);
    }

    public async Task WriteFileAsync(
        GuideEnvironment environment,
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var encoded = Convert.ToBase64String(content);
            var wslTemporary = $"{path}.monica-{Guid.NewGuid():N}.tmp";
            var script = BuildWslWriteScript(path, wslTemporary, encoded);
            var result = await RunWslAsync(environment, "sh", ["-lc", script], DefaultProcessTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new IOException(result.StandardError);
            }
            return;
        }

        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(path, out var wslPath))
            {
                throw new InvalidOperationException($"Cannot map Windows path into WSL: {path}");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(wslPath)!);
            var windowsTemporary = $"{wslPath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(windowsTemporary, content, cancellationToken);
            File.Move(windowsTemporary, wslPath, overwrite: true);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temporary, content, cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    public async Task DeleteFileAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = await RunWslAsync(environment, "rm", ["-f", "--", path], DefaultProcessTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new IOException(result.StandardError);
            }
        }
        else if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(path, out var wslPath))
            {
                throw new InvalidOperationException($"Cannot map Windows path into WSL: {path}");
            }
            if (File.Exists(wslPath)) File.Delete(wslPath);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async Task DeleteDirectoryAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = await RunWslAsync(environment, "rmdir", ["--", path], DefaultProcessTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new IOException(result.StandardError);
            }
        }
        else if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(path, out var wslPath))
            {
                throw new InvalidOperationException($"Cannot map Windows path into WSL: {path}");
            }
            // The non-recursive delete keeps the empty-only guarantee atomic on the host side.
            if (Directory.Exists(wslPath)) Directory.Delete(wslPath);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path);
        }
    }

    internal static string BuildWslWriteScript(
        string path,
        string temporaryPath,
        string encodedContent)
        => $"mkdir -p -- \"$(dirname -- {ShellQuote(path)})\" && " +
           $"printf %s {ShellQuote(encodedContent)} | base64 -d > {ShellQuote(temporaryPath)} && " +
           $"mv -f -- {ShellQuote(temporaryPath)} {ShellQuote(path)}";

    /// <summary>Builds one WSL script that writes many files, chunked by the command-line budget.</summary>
    internal static IEnumerable<string> BuildWslBatchWriteScripts(
        IReadOnlyList<(string Path, byte[] Content)> files)
    {
        var script = new StringBuilder();
        var length = 0;
        const int budget = MaxScriptCharacters;
        foreach (var (path, content) in files)
        {
            var encoded = Convert.ToBase64String(content);
            var block = BuildWslWriteScript(path, $"{path}.monica-{Guid.NewGuid():N}.tmp", encoded);
            // The separator keeps blocks independent: one failed write must not skip the rest.
            block += "; ";
            if (length > 0 && length + block.Length > budget)
            {
                yield return script.ToString();
                script.Clear();
                length = 0;
            }
            script.Append(block);
            length += block.Length;
        }
        if (length > 0)
        {
            yield return script.ToString();
        }
    }

    public async Task WriteFilesAsync(
        GuideEnvironment environment,
        IReadOnlyList<(string Path, byte[] Content)> files,
        CancellationToken cancellationToken)
    {
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            foreach (var script in BuildWslBatchWriteScripts(files))
            {
                var result = await RunWslAsync(environment, "sh", ["-lc", script], DefaultProcessTimeout, cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new IOException(result.StandardError);
                }
            }
            return;
        }

        foreach (var (path, content) in files)
        {
            await WriteFileAsync(environment, path, content, cancellationToken);
        }
    }

    public async Task CreateDirectoryAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = await RunWslAsync(environment, "mkdir", ["-p", "--", path], DefaultProcessTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new IOException(result.StandardError);
            }
            return;
        }
        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(path, out var wslPath))
            {
                throw new InvalidOperationException($"Cannot map Windows path into WSL: {path}");
            }
            Directory.CreateDirectory(wslPath);
            return;
        }
        Directory.CreateDirectory(path);
    }

    public async Task DeleteTreeAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = await RunWslAsync(environment, "rm", ["-rf", "--", path], DefaultProcessTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new IOException(result.StandardError);
            }
            return;
        }
        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(path, out var wslPath))
            {
                throw new InvalidOperationException($"Cannot map Windows path into WSL: {path}");
            }
            if (Directory.Exists(wslPath)) Directory.Delete(wslPath, recursive: true);
            return;
        }
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    public async Task MoveDirectoryAsync(
        GuideEnvironment environment,
        string fromPath,
        string toPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = await RunWslAsync(environment, "mv", ["-f", "--", fromPath, toPath], DefaultProcessTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new IOException(result.StandardError);
            }
            return;
        }
        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            if (!GuideHostPath.TryConvertWindowsPathToWsl(fromPath, out var wslFrom)
                || !GuideHostPath.TryConvertWindowsPathToWsl(toPath, out var wslTo))
            {
                throw new InvalidOperationException($"Cannot map Windows paths into WSL: {fromPath} -> {toPath}");
            }
            Directory.Move(wslFrom, wslTo);
            return;
        }
        Directory.Move(fromPath, toPath);
    }

    public async Task<string?> CommandVersionAsync(
        GuideEnvironment environment,
        string command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunAsync(environment, command, ["--version"], ShortProcessTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                return null;
            }
            var line = result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(line) ? null : line;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or Win32Exception)
        {
            return null;
        }
    }

    public string UserHome(GuideEnvironment environment)
    {
        if (environment.Kind == "wsl" && OperatingSystem.IsWindows())
        {
            var result = RunWsl(environment, "sh", ["-lc", "printf %s \"$HOME\""]);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                throw new InvalidOperationException($"Cannot resolve HOME in {environment.Selector}: {result.StandardError}");
            }
            return result.StandardOutput.Trim();
        }

        if (environment.Kind == "windows" && host.Platform == GuideHostPlatform.Wsl)
        {
            var result = RunLocal("cmd.exe", ["/d", "/c", "echo", "%USERPROFILE%"]);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
                throw new InvalidOperationException("Cannot resolve the Windows user profile from WSL.");
            return result.StandardOutput.Trim();
        }

        return host.UserHomeDirectory;
    }

    private static GuideProcessResult RunWsl(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments)
    {
        var wslArguments = BuildWslArguments(environment, executable, arguments);
        return RunLocal("wsl.exe", wslArguments);
    }

    private static Task<GuideProcessResult> RunWslAsync(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => RunLocalAsync("wsl.exe", BuildWslArguments(environment, executable, arguments), timeout, cancellationToken);

    private static List<string> BuildWslArguments(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments)
    {
        var wslArguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(environment.Distribution) && environment.Distribution != "current")
        {
            wslArguments.AddRange(["-d", environment.Distribution]);
        }
        if (!string.IsNullOrWhiteSpace(environment.User))
        {
            wslArguments.AddRange(["--user", environment.User]);
        }
        // Plain '--' relays through the user's login shell, which pre-expands every '$'
        // token in the script before the requested shell runs it; observation scripts rely
        // on their own positional parameters and variables, so they need the direct exec
        // path that passes argv through untouched.
        wslArguments.Add("--exec");
        wslArguments.Add(executable);
        wslArguments.AddRange(arguments);
        return wslArguments;
    }

    private static GuideProcessResult RunLocal(string executable, IReadOnlyList<string> arguments)
    {
        var start = CreateLocalStartInfo(executable);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        return RunLocalStartInfo(start, executable);
    }

    private static async Task<GuideProcessResult> RunLocalAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = CreateLocalStartInfo(executable);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        return await RunLocalStartInfoAsync(start, executable, timeout, cancellationToken);
    }

    private static GuideProcessResult RunLocalRawArguments(string executable, string rawArguments)
    {
        var start = CreateLocalStartInfo(executable);
        start.Arguments = rawArguments;
        return RunLocalStartInfo(start, executable);
    }

    private async Task<GuideProcessResult> RunLocalWindowsCommandAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveLocalWindowsCommand(executable);
        if (resolved is null)
        {
            return new GuideProcessResult(127, string.Empty, $"Could not resolve Windows command '{executable}'.");
        }

        if (!GuideWindowsCommand.IsBatchFile(resolved))
        {
            return await RunLocalAsync(resolved, arguments, timeout, cancellationToken);
        }

        if (!GuideWindowsCommand.TryBuildBatchCommandLine(resolved, arguments, out var commandLine, out var error))
        {
            return new GuideProcessResult(126, string.Empty, error!);
        }

        var commandProcessor = ResolveLocalWindowsCommand("cmd.exe")
                               ?? host.GetEnvironmentVariable("COMSPEC")
                               ?? "cmd.exe";
        // The batch command line is already fully quoted for 'cmd /d /s /c'; passing it
        // through ArgumentList would re-quote it and garble cmd's parsing.
        var start = CreateLocalStartInfo(commandProcessor);
        start.Arguments = $"/d /s /v:off /c {commandLine}";
        return await RunLocalStartInfoAsync(start, commandProcessor, timeout, cancellationToken);
    }

    private static ProcessStartInfo CreateLocalStartInfo(string executable)
        => new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

    private static GuideProcessResult RunLocalStartInfo(ProcessStartInfo start, string executable)
        => RunLocalStartInfoAsync(start, executable, DefaultProcessTimeout, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    private static async Task<GuideProcessResult> RunLocalStartInfoAsync(
        ProcessStartInfo start,
        string executable,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return new GuideProcessResult(127, string.Empty, $"Could not start {executable}.");
            }
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                }
                catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or TimeoutException)
                {
                }
                // A killed wsl.exe relay can leave its Linux child alive and holding the
                // redirected pipes; never block the guide on those orphaned streams.
                var timedOutOutput = await AwaitBoundedStreamAsync(stdoutTask);
                var timedOutError = await AwaitBoundedStreamAsync(stderrTask);
                return new GuideProcessResult(
                    124,
                    timedOutOutput,
                    string.IsNullOrWhiteSpace(timedOutError)
                        ? $"{executable} timed out after {(int)timeout.TotalSeconds} seconds."
                        : timedOutError);
            }
            var stdout = await AwaitBoundedStreamAsync(stdoutTask);
            var stderr = await AwaitBoundedStreamAsync(stderrTask);
            return new GuideProcessResult(process.ExitCode, stdout, stderr);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new GuideProcessResult(127, string.Empty, exception.Message);
        }
    }

    private static async Task<string> AwaitBoundedStreamAsync(Task<string> streamTask)
    {
        try
        {
            var completed = await Task.WhenAny(streamTask, Task.Delay(5_000));
            return completed == streamTask && streamTask.Status == TaskStatus.RanToCompletion
                ? streamTask.Result
                : string.Empty;
        }
        catch (Exception exception) when (exception is AggregateException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static GuideFileSystemEntryKind? LocalEntryKind(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0
                ? GuideFileSystemEntryKind.Redirect
                : (attributes & FileAttributes.Directory) != 0
                    ? GuideFileSystemEntryKind.Directory
                    : GuideFileSystemEntryKind.File;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static async Task<string?> HashLocalFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var digest = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(digest).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<GuideFileSystemEntry>> ListLocalEntriesAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        var entries = new List<GuideFileSystemEntry>();
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                var kind = (attributes & FileAttributes.ReparsePoint) != 0
                    ? GuideFileSystemEntryKind.Redirect
                    : (attributes & FileAttributes.Directory) != 0
                        ? GuideFileSystemEntryKind.Directory
                        : GuideFileSystemEntryKind.File;
                var resultPath = entry;
                if (environment.Kind == "windows"
                    && host.Platform == GuideHostPlatform.Wsl
                    && GuideHostPath.TryConvertWslPathToWindows(entry, out var windowsPath))
                {
                    resultPath = windowsPath;
                }
                entries.Add(new GuideFileSystemEntry(resultPath, kind));
                if (kind == GuideFileSystemEntryKind.Directory)
                {
                    pending.Push(entry);
                }
            }
        }
        return entries
            .OrderBy(
                static entry => entry.Path,
                environment.Kind == "windows" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> EnumerateContainedPath(
        GuideEnvironment environment,
        string root,
        string path)
    {
        if (environment.Kind == "wsl")
        {
            var normalizedRoot = root.TrimEnd('/');
            var normalizedPath = path.TrimEnd('/');
            if (!normalizedPath.Equals(normalizedRoot, StringComparison.Ordinal)
                && !normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Target path escapes the selected root: {path}");
            }

            var result = new List<string> { normalizedRoot };
            var relative = normalizedPath.Length == normalizedRoot.Length
                ? string.Empty
                : normalizedPath[(normalizedRoot.Length + 1)..];
            var current = normalizedRoot;
            foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment is "." or "..")
                {
                    throw new InvalidOperationException($"Target path is not normalized: {path}");
                }
                current = $"{current}/{segment}";
                result.Add(current);
            }
            return result;
        }

        var normalizedRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedTargetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var comparison = environment.Kind == "windows"
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!normalizedTargetPath.Equals(normalizedRootPath, comparison)
            && !normalizedTargetPath.StartsWith(normalizedRootPath + Path.DirectorySeparatorChar, comparison))
        {
            throw new InvalidOperationException($"Target path escapes the selected root: {path}");
        }

        var paths = new List<string> { normalizedRootPath };
        var relativePath = Path.GetRelativePath(normalizedRootPath, normalizedTargetPath);
        var currentPath = normalizedRootPath;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "..")
            {
                throw new InvalidOperationException($"Target path escapes the selected root: {path}");
            }
            // A "." segment (path equals the root) adds nothing below the anchor.
            if (segment is ".")
            {
                continue;
            }

            currentPath = Path.Combine(currentPath, segment);
            paths.Add(currentPath);
        }
        return paths;
    }

    internal static GuideFileSystemEntryKind? ParseEntryKind(char marker)
        => marker switch
        {
            '\0' => null,
            // 'm' is the observation script's "path absent" marker: absent must stay null so
            // planners treat it as "create fresh", never as an existing conflicting entry.
            'm' => null,
            'f' => GuideFileSystemEntryKind.File,
            'd' => GuideFileSystemEntryKind.Directory,
            'l' => GuideFileSystemEntryKind.Redirect,
            'o' => GuideFileSystemEntryKind.Other,
            // An unknown marker means the observation stream is corrupt; guessing either
            // "absent" or "exists" here silently blocks or silently overwrites real state.
            _ => throw new InvalidDataException($"Unknown WSL observation kind marker '{marker}'.")
        };
}

internal sealed record GuideProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed record GuideFileSystemEntry(
    string Path,
    GuideFileSystemEntryKind Kind);

internal enum GuideFileSystemEntryKind
{
    File,
    Directory,
    Redirect,
    Other
}

using System.Security.Cryptography;
using Monica.Guide;

namespace Test.Monica.Guide;

internal sealed class FakeRuntime(string home) : IGuideEnvironmentRuntime
{
    internal static GuideEnvironment WindowsEnvironment { get; } = new("windows", "windows");
    internal string Home { get; } = home;
    internal HashSet<string> FailNextDeletesOf { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal bool NoCommands { get; set; }
    internal HashSet<string> UnavailableCommands { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> RedirectedPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GuideEnvironment> DetectEnvironments() => [WindowsEnvironment];

    public bool CommandExists(GuideEnvironment environment, string command)
        => !NoCommands
           && !UnavailableCommands.Contains(command)
           && command is ("codex" or "claude");

    public Task<string?> CommandVersionAsync(
        GuideEnvironment environment,
        string command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(
            !CommandExists(environment, command)
                ? null
                : command switch
                {
                    "codex" => "codex-cli 0.147.0",
                    "claude" => "2.1.221 (Claude Code)",
                    _ => null
                });
    }

    public Task<GuideProcessResult> RunAsync(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
        // The guide is pure skill management: it never shells out to host CLIs, so the
        // fake fails loudly if production code regresses into running host commands.
        => Task.FromResult(new GuideProcessResult(
            127,
            string.Empty,
            "the guide must not run host commands"));

    public Task<GuideProcessResult> RunAsync(
        GuideEnvironment environment,
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => RunAsync(environment, executable, arguments, cancellationToken);

    public async Task<GuideTreeObservation> ObserveTreeAsync(
        GuideEnvironment environment,
        string homeRoot,
        IReadOnlyList<string> observePaths,
        IReadOnlyList<string> digestFiles,
        IReadOnlyList<string> listRoots,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, GuidePathObservation>(StringComparer.OrdinalIgnoreCase);
        var digests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rootEntries = new Dictionary<string, IReadOnlyList<GuideFileSystemEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in observePaths.Distinct())
        {
            paths[path] = new GuidePathObservation(
                await HasPathRedirectionAsync(environment, homeRoot, path, cancellationToken),
                await GetEntryKindAsync(environment, path, cancellationToken));
        }
        foreach (var file in digestFiles.Distinct())
        {
            var bytes = await ReadFileAsync(environment, file, cancellationToken);
            if (bytes is not null)
            {
                digests[file] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            }
        }
        foreach (var root in listRoots.Distinct())
        {
            rootEntries[root] = await ListEntriesAsync(environment, root, cancellationToken);
        }
        return new GuideTreeObservation(paths, digests, rootEntries);
    }

    public Task<string?> GetEnvironmentVariableAsync(
        GuideEnvironment environment,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }

    public async Task<byte[]?> ReadFileAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
        => File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;

    public Task<GuideFileSystemEntryKind?> GetEntryKindAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RedirectedPaths.Contains(path))
        {
            return Task.FromResult<GuideFileSystemEntryKind?>(
                GuideFileSystemEntryKind.Redirect);
        }
        if (Directory.Exists(path))
        {
            return Task.FromResult<GuideFileSystemEntryKind?>(
                GuideFileSystemEntryKind.Directory);
        }
        return Task.FromResult<GuideFileSystemEntryKind?>(
            File.Exists(path) ? GuideFileSystemEntryKind.File : null);
    }

    public Task<IReadOnlyList<GuideFileSystemEntry>> ListEntriesAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(path))
        {
            return Task.FromResult<IReadOnlyList<GuideFileSystemEntry>>([]);
        }
        var entries = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
            .Select(entry => new GuideFileSystemEntry(
                entry,
                Directory.Exists(entry)
                    ? GuideFileSystemEntryKind.Directory
                    : GuideFileSystemEntryKind.File))
            .OrderBy(static entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<GuideFileSystemEntry>>(entries);
    }

    public Task<bool> HasPathRedirectionAsync(
        GuideEnvironment environment,
        string root,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RedirectedPaths.Any(redirected =>
            path.Equals(redirected, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(
                redirected.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || redirected.StartsWith(
                path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)));
    }

    public async Task WriteFileAsync(
        GuideEnvironment environment,
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public async Task WriteFilesAsync(
        GuideEnvironment environment,
        IReadOnlyList<(string Path, byte[] Content)> files,
        CancellationToken cancellationToken)
    {
        foreach (var (path, content) in files)
        {
            await WriteFileAsync(environment, path, content, cancellationToken);
        }
    }

    public Task CreateDirectoryAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task DeleteTreeAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailNextDeletesOf.Remove(path))
        {
            throw new IOException("Injected delete failure.");
        }
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        else if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailNextDeletesOf.Remove(path))
        {
            throw new IOException("Injected delete failure.");
        }
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(
        GuideEnvironment environment,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Non-recursive on purpose: an unexpected non-empty directory must fail loudly.
        if (Directory.Exists(path)) Directory.Delete(path);
        return Task.CompletedTask;
    }

    public Task MoveDirectoryAsync(
        GuideEnvironment environment,
        string fromPath,
        string toPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.Move(fromPath, toPath);
        return Task.CompletedTask;
    }

    public string UserHome(GuideEnvironment environment) => Home;
}

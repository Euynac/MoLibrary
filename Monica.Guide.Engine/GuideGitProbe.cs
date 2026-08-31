using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Monica.Guide;

/// <summary>
/// Read-only Git identity of one local checkout: the worktree root and the canonical
/// remote. Cheap to observe; no commit resolution and no worktree status scan.
/// </summary>
public sealed record GuideGitIdentity(
    string Root,
    string? CanonicalRemote);

/// <summary>Read-only Git facts about one local checkout.</summary>
public sealed record GuideGitInfo(
    string Commit,
    string Root,
    string? CanonicalRemote,
    bool Dirty);

/// <summary>
/// Read-only Git access for workspace detection and source binding verification. Every
/// operation is a short-lived <c>git</c> invocation against a local repository; nothing
/// here fetches, writes, or authenticates.
/// </summary>
public interface IGuideGitProbe
{
    /// <summary>Returns null when the path is not inside a Git working tree.</summary>
    GuideGitInfo? Describe(string path);

    /// <summary>
    /// Resolves only the worktree root and canonical remote. Detection runs before every
    /// wizard interaction, so it must not pay for the commit resolution and worktree
    /// status scan that <see cref="Describe"/> performs; callers that need those facts
    /// still call <see cref="Describe"/>.
    /// </summary>
    GuideGitIdentity? FindIdentity(string path);

    /// <summary>Resolves one exact tag name to its commit; null when the tag does not exist.</summary>
    string? ResolveTagCommit(string repositoryRoot, string tag);
}

public sealed partial class GuideGitProbe : IGuideGitProbe
{
    private static readonly TimeSpan COMMAND_TIMEOUT = TimeSpan.FromSeconds(15);

    public GuideGitInfo? Describe(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = Run(path, "rev-parse --show-toplevel");
        if (root is null)
        {
            return null;
        }

        root = Path.GetFullPath(root.Trim());
        var commit = Run(root, "rev-parse HEAD")?.Trim().ToLowerInvariant();
        if (commit is null || !IsCommit(commit))
        {
            return null;
        }

        var remote = Run(root, "remote get-url origin")?.Trim();
        var status = Run(root, "status --porcelain");
        return new GuideGitInfo(commit, root, CanonicalRepository(remote), !string.IsNullOrEmpty(status));
    }

    public GuideGitIdentity? FindIdentity(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = Run(path, "rev-parse --show-toplevel");
        if (root is null)
        {
            return null;
        }

        var remote = Run(root, "remote get-url origin")?.Trim();
        return new GuideGitIdentity(Path.GetFullPath(root.Trim()), CanonicalRepository(remote));
    }

    public string? ResolveTagCommit(string repositoryRoot, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var commit = Run(repositoryRoot, $"rev-parse --verify refs/tags/{tag}^{{commit}}")?.Trim().ToLowerInvariant();
        return commit is not null && IsCommit(commit) ? commit : null;
    }

    internal static bool IsCommit(string value)
        => value.Length == 40 && value.All(char.IsAsciiHexDigit);

    /// <summary>
    /// Reduces a Git remote URL to its canonical <c>owner/name</c> form. SSH and HTTPS
    /// GitHub remotes are equivalent; non-GitHub remotes return null.
    /// </summary>
    internal static string? CanonicalRepository(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        var match = CanonicalRemotePattern().Match(remoteUrl.Trim());
        return match.Success ? $"{match.Groups[1].Value}/{match.Groups[2].Value}" : null;
    }

    [GeneratedRegex(@"^(?:git@github\.com:|https?://(?:[^@]+@)?github\.com/)([\w.-]+)/([\w.-]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalRemotePattern();

    private static string? Run(string workingDirectory, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{workingDirectory}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return null;
            }

            // Drain the pipe while waiting; a large status listing would otherwise fill the
            // buffer and deadlock the child process before WaitForExit returns.
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit((int)COMMAND_TIMEOUT.TotalMilliseconds))
            {
                process.Kill();
                return null;
            }

            return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}

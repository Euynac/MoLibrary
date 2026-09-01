using Monica.Guide;

namespace Test.Monica.Guide;

/// <summary>
/// A Git probe that describes exactly one directory as a canonical <c>Tairitsua/Monica</c>
/// checkout with a fixed commit, for source-binding fixtures.
/// </summary>
internal sealed class CanonicalCheckoutProbe(string root, string remote = "https://github.com/Tairitsua/Monica.git")
    : IGuideGitProbe
{
    internal const string Commit = "0123456789abcdef0123456789abcdef01234567";

    public GuideGitInfo? Describe(string path)
        => string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
            ? new GuideGitInfo(Commit, root, GuideGitProbe.CanonicalRepository(remote), false)
            : null;

    public GuideGitIdentity? FindIdentity(string path) => null;

    public string? ResolveTagCommit(string repositoryRoot, string tag) => null;
}

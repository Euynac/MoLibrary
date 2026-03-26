namespace Monica.DevOps.Git.Services;

/// <summary>
/// Normalizes Git remote URLs for repository matching.
/// </summary>
internal static class GitRemoteUrlNormalizer
{
    public static string Normalize(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return string.Empty;
        }

        var value = remoteUrl.Trim().Replace('\\', '/');

        if (value.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var withoutUser = value[4..];
            var separatorIndex = withoutUser.IndexOf(':');
            if (separatorIndex > 0)
            {
                var host = withoutUser[..separatorIndex];
                var path = withoutUser[(separatorIndex + 1)..].Trim('/');
                return NormalizePath($"{host}/{path}");
            }
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return NormalizePath($"{uri.Host}{uri.AbsolutePath}");
        }

        return NormalizePath(value);
    }

    private static string NormalizePath(string value)
    {
        var normalized = value.Trim().Trim('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized.ToLowerInvariant();
    }
}

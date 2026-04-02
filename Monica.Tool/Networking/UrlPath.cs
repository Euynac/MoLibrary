namespace Monica.Tool.Networking;

/// <summary>
/// Combines URL path fragments without duplicating separators.
/// </summary>
public static class UrlPath
{
    /// <summary>
    /// Combines a base URL and a relative path fragment.
    /// </summary>
    public static string Combine(string baseUrl, string appendPath)
    {
        return $"{baseUrl.TrimEnd('/')}/{appendPath.TrimStart('/')}";
    }
}

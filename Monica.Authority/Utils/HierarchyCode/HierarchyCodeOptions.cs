namespace Monica.Authority.Utils.HierarchyCode;

/// <summary>
/// Defines immutable hierarchy-code conventions used by Monica authority models.
/// </summary>
public static class HierarchyCodeOptions
{
    /// <summary>
    /// Length of each code segment (default: 5) The CodeLength property determines how many digits each segment of the hierarchy code will have, padded with leading zeros. Its main purpose is to ensure proper sorting in databases and UI displays. If a number exceeds the CodeLength, it will still work properly.
    /// </summary>
    public const int CodeLength = 5;

    /// <summary>
    /// Maximum depth of hierarchy (default: 10)
    /// </summary>
    public const int MaxDepth = 10;

    /// <summary>
    /// Whether to use relative depth mode. When true, codes can exceed MaxDepth by removing the oldest segments.
    /// When false, codes exceeding MaxDepth will throw an exception.
    /// </summary>
    public static bool UseRelativeDepth => false;
}

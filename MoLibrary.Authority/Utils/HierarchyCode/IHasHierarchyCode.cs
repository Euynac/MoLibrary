namespace MoLibrary.Authority.Utils.HierarchyCode;

/// <summary>
/// Interface for entities that support hierarchical coding
/// </summary>
public interface IHasHierarchyCode
{
    /// <summary>
    /// Hierarchical Code of this entity.
    /// Example: "00001.00042.00005".
    /// This is a unique code for an entity in a hierarchy.
    /// It's changeable if hierarchy is changed.
    /// </summary>
    string Code { get; set; }
}

/// <summary>
/// Configuration for hierarchy code generation
/// </summary>
public static class HierarchyCodeOptions
{
    /// <summary>
    /// Length of each code segment (default: 5) The CodeLength property determines how many digits each segment of the hierarchy code will have, padded with leading zeros. Its main purpose is to ensure proper sorting in databases and UI displays. If a number exceeds the CodeLength, it will still work properly.
    /// </summary>
    public static int CodeLength { get; set; } = 5;

    /// <summary>
    /// Maximum depth of hierarchy (default: 10)
    /// </summary>
    public static int MaxDepth { get; set; } = 10;

    /// <summary>
    /// Whether to use relative depth mode. When true, codes can exceed MaxDepth by removing the oldest segments.
    /// When false, codes exceeding MaxDepth will throw an exception.
    /// </summary>
    public static bool UseRelativeDepth { get; set; } = false;
} 
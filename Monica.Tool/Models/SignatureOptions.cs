namespace Monica.Tool.Models;

/// <summary>
/// Configures how signature payloads are built from an object graph.
/// </summary>
public class SignatureOptions
{
    /// <summary>
    /// When enabled, only properties marked with <c>SignatureOnlyAttribute</c>
    /// participate in the signature payload.
    /// </summary>
    public bool UseSignatureOnlyAttributes { get; set; }

    /// <summary>
    /// Skips properties whose values are <see langword="null"/>.
    /// </summary>
    public bool IgnoreNullValues { get; set; } = true;

    /// <summary>
    /// Additional property names to exclude from the signature payload.
    /// </summary>
    public HashSet<string> IgnoredPropertyNames { get; set; } = [];

    /// <summary>
    /// Sorts output keys using ordinal string comparison before serialization.
    /// </summary>
    public bool SortByAscii { get; set; } = true;

    /// <summary>
    /// Lowercases the final hash text.
    /// </summary>
    public bool LowercaseHash { get; set; } = true;

    /// <summary>
    /// Lowercases serialized field names before concatenation.
    /// </summary>
    public bool LowercaseKeys { get; set; } = true;

    /// <summary>
    /// Chooses the serialization format used before hashing.
    /// </summary>
    public SignatureMode Mode { get; set; } = SignatureMode.QueryString;
}

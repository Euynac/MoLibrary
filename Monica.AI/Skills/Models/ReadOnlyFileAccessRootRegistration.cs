namespace Monica.AI.Skills.Models;

/// <summary>
/// Describes one filesystem root that Monica agents may inspect through the read-only file access skill.
/// </summary>
/// <remarks>
/// The root name is the stable identifier supplied to skill tools. The path may be absolute or relative to the
/// running application directory; all tool calls still use relative paths inside this root.
/// </remarks>
public sealed record ReadOnlyFileAccessRootRegistration
{
    /// <summary>
    /// Creates a read-only file access root registration.
    /// </summary>
    /// <param name="name">Stable root identifier used by tool calls. Root names are compared case-insensitively.</param>
    /// <param name="path">Absolute path, or path relative to the running application directory, that agents may inspect.</param>
    /// <param name="description">Optional human-readable purpose shown to agents when roots are listed.</param>
    public ReadOnlyFileAccessRootRegistration(string name, string path, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Name = NormalizeName(name);
        Path = path.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>
    /// Stable root identifier supplied to skill tools.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Absolute path, or path relative to the running application directory, that defines the inspectable root.
    /// </summary>
    public string Path { get; init; }

    /// <summary>
    /// Optional human-readable purpose shown to agents when roots are listed.
    /// </summary>
    public string? Description { get; init; }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Any(static ch => char.IsControl(ch) || ch is '/' or '\\'))
        {
            throw new ArgumentException("Read-only file access root names cannot contain path separators or control characters.", nameof(name));
        }

        return normalized;
    }
}

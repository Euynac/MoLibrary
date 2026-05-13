namespace Monica.Configuration.Models;

/// <summary>
/// Base type for a segment in a structured logical configuration path.
/// </summary>
public abstract record ConfigurationPathSegment
{
    /// <summary>
    /// Gets a display value used by diagnostics and canonical formatting.
    /// </summary>
    public abstract string Value { get; }
}

/// <summary>
/// Represents a CLR property segment.
/// </summary>
/// <param name="Name">The property name.</param>
public sealed record PropertySegment(string Name) : ConfigurationPathSegment
{
    /// <inheritdoc />
    public override string Value => Name;
}

/// <summary>
/// Represents a dictionary key segment.
/// </summary>
/// <param name="Key">The dictionary key.</param>
public sealed record DictionaryKeySegment(string Key) : ConfigurationPathSegment
{
    /// <inheritdoc />
    public override string Value => Key;
}

/// <summary>
/// Represents a stable list item key segment.
/// </summary>
/// <param name="ItemKey">The stable item key.</param>
public sealed record ListItemKeySegment(string ItemKey) : ConfigurationPathSegment
{
    /// <inheritdoc />
    public override string Value => ItemKey;
}

/// <summary>
/// Represents a numeric list index segment used only for projection and diagnostics.
/// </summary>
/// <param name="Index">The projected list index.</param>
public sealed record ListIndexSegment(int Index) : ConfigurationPathSegment
{
    /// <inheritdoc />
    public override string Value => Index.ToString();
}

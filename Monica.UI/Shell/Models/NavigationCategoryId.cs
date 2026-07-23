namespace Monica.UI.Shell.Models;

/// <summary>
/// Identifies a navigation category independently of its localized display text.
/// </summary>
/// <remarks>
/// Category identity is case-insensitive and <see cref="Value"/> stores a lowercase canonical representation.
/// Use a durable, publisher-qualified identifier for package-owned categories so localization changes cannot alter
/// grouping or ordering behavior.
/// </remarks>
public sealed class NavigationCategoryId : IEquatable<NavigationCategoryId>
{
    private const int MAXIMUM_ID_LENGTH = 100;

    private NavigationCategoryId(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the canonical lowercase identifier.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a navigation category identifier.
    /// </summary>
    /// <param name="value">
    /// A dot-separated identifier whose segments begin with an ASCII letter and contain only ASCII letters or digits.
    /// Package-owned categories should normally use their package or module identity.
    /// </param>
    /// <returns>The validated category identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not a valid identifier.</exception>
    public static NavigationCategoryId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MAXIMUM_ID_LENGTH)
        {
            throw new ArgumentException(
                $"A navigation category identifier cannot exceed {MAXIMUM_ID_LENGTH} characters.",
                nameof(value));
        }

        var segments = value.Split('.');
        if (segments.Length < 2 || segments.Any(static segment => !IsValidSegment(segment)))
        {
            throw new ArgumentException(
                "A navigation category identifier must contain at least two dot-separated ASCII letter-or-digit " +
                "segments, and every segment must begin with a letter.",
                nameof(value));
        }

        return new NavigationCategoryId(value.ToLowerInvariant());
    }

    /// <inheritdoc />
    public bool Equals(NavigationCategoryId? other) =>
        other is not null && StringComparer.Ordinal.Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is NavigationCategoryId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>
    /// Determines whether two identifiers represent the same category.
    /// </summary>
    public static bool operator ==(NavigationCategoryId? left, NavigationCategoryId? right) =>
        ReferenceEquals(left, right) || left?.Equals(right) == true;

    /// <summary>
    /// Determines whether two identifiers represent different categories.
    /// </summary>
    public static bool operator !=(NavigationCategoryId? left, NavigationCategoryId? right) => !(left == right);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsValidSegment(string segment)
    {
        if (segment.Length == 0 || !IsAsciiLetter(segment[0]))
        {
            return false;
        }

        foreach (var character in segment.AsSpan(1))
        {
            if (!IsAsciiLetter(character) && !char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}

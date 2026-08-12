using System.Globalization;

namespace Monica.Core.JsonSerialization.Models;

/// <summary>
/// Defines a supported wire representation for timezone-free <see cref="DateTime" /> wall-clock values.
/// </summary>
/// <remarks>
/// This policy changes only the separator used for outbound values. Both supported representations are accepted
/// on input so hosts can interoperate safely while moving between policies. Use <see cref="DateTimeOffset" />
/// when a value represents an instant or carries an offset.
/// </remarks>
public sealed class DateTimeWireFormat
{
    private const string ISO_8601_WALL_CLOCK_PATTERN = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";
    private const string SPACE_SEPARATED_WALL_CLOCK_PATTERN = "yyyy-MM-dd HH:mm:ss.FFFFFFF";

    private static readonly string[] ACCEPTED_INPUT_PATTERNS =
    [
        ISO_8601_WALL_CLOCK_PATTERN,
        SPACE_SEPARATED_WALL_CLOCK_PATTERN,
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd"
    ];

    private DateTimeWireFormat(string pattern)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// Gets the ISO 8601-style wall-clock representation that separates the date and time with <c>T</c>.
    /// </summary>
    public static DateTimeWireFormat Iso8601WallClock { get; } = new(ISO_8601_WALL_CLOCK_PATTERN);

    /// <summary>
    /// Gets the wall-clock representation that separates the date and time with a space.
    /// </summary>
    public static DateTimeWireFormat SpaceSeparatedWallClock { get; } = new(SPACE_SEPARATED_WALL_CLOCK_PATTERN);

    /// <summary>
    /// Gets the invariant custom date and time format string used for outbound values.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Formats a wall-clock value without emitting its <see cref="DateTime.Kind" /> or applying a timezone conversion.
    /// </summary>
    /// <param name="value">The wall-clock value to format.</param>
    /// <returns>The invariant wire representation.</returns>
    public string Format(DateTime value) => value.ToString(Pattern, CultureInfo.InvariantCulture);

    internal static bool TryParse(string? value, out DateTime result)
    {
        return DateTime.TryParseExact(
            value,
            ACCEPTED_INPUT_PATTERNS,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    /// <inheritdoc />
    public override string ToString() => Pattern;
}

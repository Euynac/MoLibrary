using System.Text;
using System.Globalization;
using Monica.Tool.Extensions;

namespace Monica.Tool.Text;

/// <summary>
/// Provides text-oriented formatting helpers for display scenarios.
/// </summary>
public static class DisplayFormatting
{
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    /// <summary>
    /// Converts a one-based alphabet index to an uppercase letter.
    /// </summary>
    public static char ToAlphabetLetter(this int index)
    {
        if (index is < 1 or > 26)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Alphabet indexes must be between 1 and 26.");
        }

        return (char)('A' + (index - 1));
    }

    /// <summary>
    /// Converts a letter to its one-based alphabet index.
    /// </summary>
    public static int ToAlphabetIndex(this char letter)
    {
        var normalizedLetter = char.ToUpperInvariant(letter);
        if (normalizedLetter is < 'A' or > 'Z')
        {
            throw new ArgumentOutOfRangeException(nameof(letter), "Alphabet conversion only supports A-Z.");
        }

        return normalizedLetter - 'A' + 1;
    }

    /// <summary>
    /// Formats the byte count as a human-readable size string.
    /// </summary>
    public static string FormatByteSize(this int bytes) => FormatByteSize((long)bytes);

    /// <summary>
    /// Formats the byte count as a human-readable size string.
    /// </summary>
    public static string FormatByteSize(this long bytes)
    {
        double size = bytes;
        var unitIndex = 0;
        while (unitIndex < SizeUnits.Length - 1 && Math.Abs(size) >= 1024)
        {
            size /= 1024;
            unitIndex++;
        }

        return size.ToString("0.###", CultureInfo.InvariantCulture) + SizeUnits[unitIndex];
    }

    /// <summary>
    /// Formats a text progress bar such as <c>[||||||    ]</c>.
    /// </summary>
    public static string FormatProgressBar(int length, double percentage)
    {
        if (length < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Progress bar length must be at least 3.");
        }

        if (!double.IsFinite(percentage))
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be finite.");
        }

        var completedCount = (int)((length - 2) * percentage.LimitInRange(0, 1));
        var remainingCount = length - 2 - completedCount;
        var builder = new StringBuilder(length);
        builder.Append('[');
        builder.Append('|', completedCount);
        builder.Append(' ', remainingCount);
        builder.Append(']');
        return builder.ToString();
    }
}

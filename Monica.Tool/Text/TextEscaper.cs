using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Monica.Tool.Text;

/// <summary>
/// Provides text escaping helpers for regex and serialized text processing.
/// </summary>
public static class TextEscaper
{
    /// <summary>
    /// The same as Regex.Escape.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static string ToRegexEscaped(this string pattern) => Regex.Escape(pattern);

    /// <summary>
    /// Converts non-ASCII UTF-16 code units to uppercase <c>\uXXXX</c> escape sequences while leaving ASCII regex
    /// syntax unchanged.
    /// </summary>
    /// <param name="pattern">The regular expression pattern to normalize.</param>
    /// <returns>The normalized regular expression pattern.</returns>
    public static string ToRegexUnicodeEscaped(this string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return pattern;
        }

        var result = new StringBuilder(pattern.Length);
        foreach (var character in pattern)
        {
            if (character <= 0x7F)
            {
                result.Append(character);
                continue;
            }

            result.Append(@"\u");
            result.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }

    /// <summary>
    /// Doing Regex.Escape and also escape ])}.
    /// <para></para>
    /// https://github.com/dotnet/runtime/issues/45896
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public static string ToRegexEscapedAll(this string pattern)
    {
        return Regex.Escape(pattern).Replace("]", "\\]").Replace("}", "\\}").Replace(")", "\\)");
    }
    private enum RemoveEscapeCharsStates
    {
        Reset,
        FoundEscapeChar
    }
    /// <summary>
    /// Remove escape chars from text.
    /// </summary>
    /// <param name="originalText"></param>
    /// <param name="escapeChar"></param>
    /// <returns></returns>
    public static string RemoveEscapeChars(string originalText, char escapeChar = '\\')
    {
        ArgumentNullException.ThrowIfNull(originalText);

        var result = new StringBuilder(originalText.Length);
        var escaping = false;
        foreach (var chr in originalText)
        {
            if (escaping)
            {
                result.Append(chr);
                escaping = false;
            }
            else if (chr == escapeChar)
            {
                escaping = true;
            }
            else
            {
                result.Append(chr);
            }
        }

        if (escaping)
        {
            throw new FormatException("The text ends with an incomplete escape sequence.");
        }

        return result.ToString();
    }
}

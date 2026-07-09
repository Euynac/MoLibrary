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
        var result = new StringBuilder();
        var state = RemoveEscapeCharsStates.Reset;
        foreach (var chr in originalText)
        {
            switch (state)
            {
                case RemoveEscapeCharsStates.Reset:
                    if (chr == escapeChar)
                    {
                        state = RemoveEscapeCharsStates.FoundEscapeChar;
                    }
                    else
                    {
                        result.Append(chr);
                        state = RemoveEscapeCharsStates.Reset;
                    }
                    break;
                case RemoveEscapeCharsStates.FoundEscapeChar:
                    result.Append(chr);
                    state = RemoveEscapeCharsStates.Reset;
                    break;
                default:
                    throw new Exception("Unknown state");
            }
        }
        if (state != RemoveEscapeCharsStates.Reset)
        {
            throw new Exception($"{state} is not an accept state!");
        }
        return result.ToString();
    }
}

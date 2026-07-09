namespace Monica.Configuration.UI.Support;

internal static class ConfigurationRegexPatternHighlighter
{
    private static readonly HashSet<char> SimpleEscapes =
    [
        'a',
        'A',
        'b',
        'B',
        'd',
        'D',
        'e',
        'f',
        'G',
        'n',
        'r',
        's',
        'S',
        't',
        'v',
        'w',
        'W',
        'Z',
        'z'
    ];

    public static IReadOnlyList<ConfigurationRegexPatternToken> Tokenize(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return [];
        }

        var tokens = new List<ConfigurationRegexPatternToken>();
        var textStart = 0;
        for (var index = 0; index < pattern.Length; index++)
        {
            if (pattern[index] != '\\')
            {
                continue;
            }

            AddTextToken(tokens, pattern, textStart, index);
            var escape = ReadEscape(pattern, index);
            tokens.Add(new ConfigurationRegexPatternToken(
                escape.Text,
                escape.IsValid
                    ? "configuration-regex-token-escape"
                    : "configuration-regex-token-invalid"));

            index = escape.EndIndex;
            textStart = index + 1;
        }

        AddTextToken(tokens, pattern, textStart, pattern.Length);
        return tokens;
    }

    private static ConfigurationRegexEscapeToken ReadEscape(string pattern, int start)
    {
        if (start + 1 >= pattern.Length)
        {
            return new ConfigurationRegexEscapeToken(pattern[start..], pattern.Length - 1, IsValid: false);
        }

        return pattern[start + 1] switch
        {
            'u' => ReadFixedHexEscape(pattern, start, length: 6),
            'x' => ReadFixedHexEscape(pattern, start, length: 4),
            'p' or 'P' => ReadBracedEscape(pattern, start),
            'k' => ReadNamedBackreference(pattern, start),
            'c' => start + 2 < pattern.Length
                ? new ConfigurationRegexEscapeToken(pattern.Substring(start, 3), start + 2, IsValid: true)
                : new ConfigurationRegexEscapeToken(pattern[start..], pattern.Length - 1, IsValid: false),
            var value when char.IsDigit(value) => ReadDigitEscape(pattern, start),
            var value when SimpleEscapes.Contains(value) || IsEscapedPunctuation(value) =>
                new ConfigurationRegexEscapeToken(pattern.Substring(start, 2), start + 1, IsValid: true),
            var value when char.IsLetter(value) =>
                new ConfigurationRegexEscapeToken(pattern.Substring(start, 2), start + 1, IsValid: false),
            _ => new ConfigurationRegexEscapeToken(pattern.Substring(start, 2), start + 1, IsValid: true)
        };
    }

    private static ConfigurationRegexEscapeToken ReadFixedHexEscape(string pattern, int start, int length)
    {
        var endExclusive = Math.Min(pattern.Length, start + length);
        var text = pattern[start..endExclusive];
        var isValid = pattern.Length >= start + length
                      && IsHexSpan(pattern.AsSpan(start + 2, length - 2));

        return new ConfigurationRegexEscapeToken(text, endExclusive - 1, isValid);
    }

    private static ConfigurationRegexEscapeToken ReadBracedEscape(string pattern, int start)
    {
        if (start + 2 >= pattern.Length || pattern[start + 2] != '{')
        {
            return new ConfigurationRegexEscapeToken(pattern.Substring(start, 2), start + 1, IsValid: false);
        }

        var end = pattern.IndexOf('}', start + 3);
        if (end < 0)
        {
            return new ConfigurationRegexEscapeToken(pattern[start..], pattern.Length - 1, IsValid: false);
        }

        return new ConfigurationRegexEscapeToken(pattern[start..(end + 1)], end, IsValid: end > start + 3);
    }

    private static ConfigurationRegexEscapeToken ReadNamedBackreference(string pattern, int start)
    {
        if (start + 2 >= pattern.Length)
        {
            return new ConfigurationRegexEscapeToken(pattern[start..], pattern.Length - 1, IsValid: false);
        }

        var opener = pattern[start + 2];
        var closer = opener switch
        {
            '<' => '>',
            '\'' => '\'',
            _ => '\0'
        };

        if (closer == '\0')
        {
            return new ConfigurationRegexEscapeToken(pattern.Substring(start, 2), start + 1, IsValid: false);
        }

        var end = pattern.IndexOf(closer, start + 3);
        if (end < 0)
        {
            return new ConfigurationRegexEscapeToken(pattern[start..], pattern.Length - 1, IsValid: false);
        }

        return new ConfigurationRegexEscapeToken(pattern[start..(end + 1)], end, IsValid: end > start + 3);
    }

    private static ConfigurationRegexEscapeToken ReadDigitEscape(string pattern, int start)
    {
        var end = start + 1;
        while (end + 1 < pattern.Length && char.IsDigit(pattern[end + 1]))
        {
            end++;
        }

        return new ConfigurationRegexEscapeToken(pattern[start..(end + 1)], end, IsValid: true);
    }

    private static bool IsEscapedPunctuation(char value)
    {
        return value is '\\' or '.' or '$' or '^' or '{' or '[' or '(' or '|' or ')' or '*' or '+' or '?' or ']' or '}' or '-';
    }

    private static bool IsHexSpan(ReadOnlySpan<char> span)
    {
        foreach (var value in span)
        {
            if (!IsHex(value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHex(char value)
    {
        return value is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
    }

    private static void AddTextToken(
        ICollection<ConfigurationRegexPatternToken> tokens,
        string pattern,
        int start,
        int endExclusive)
    {
        if (endExclusive <= start)
        {
            return;
        }

        tokens.Add(new ConfigurationRegexPatternToken(
            pattern[start..endExclusive],
            "configuration-regex-token-text"));
    }

    private sealed record ConfigurationRegexEscapeToken(string Text, int EndIndex, bool IsValid);
}

internal sealed record ConfigurationRegexPatternToken(string Text, string CssClass);

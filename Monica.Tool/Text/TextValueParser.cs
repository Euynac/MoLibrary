using System.Text.RegularExpressions;
using System.Globalization;
using Monica.Tool.Annotations;
using Monica.Tool.Extensions;

namespace Monica.Tool.Text;

/// <summary>
/// Parses common text values used by Monica modules.
/// </summary>
public static class TextValueParser
{
    private const string IntervalValuePattern = @"(?:(?:\d{1,7}\.)?(?:(?:2[0-3]|[01]?\d):)?(?:(?:[0-5]?\d):))?(?:[0-5]?\d)(?:\.\d{1,7})?";
    private static readonly Regex IntervalRegex = new(
        $@"^\s*(?<left>{IntervalValuePattern})\s*[^.:\d]+?\s*(?<right>{IntervalValuePattern})\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));
    private static readonly Regex IntervalValueRegex = new(
        @"^(?:(?:(?<day>\d{1,7})\.)?(?:(?<hour>2[0-3]|[01]?\d):)?(?:(?<minute>[0-5]?\d):))?(?<second>[0-5]?\d)(?:\.(?<fraction>\d{1,7}))?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    /// <summary>
    /// Tries to parse localized boolean text, including common English and Chinese literals.
    /// </summary>
    /// <param name="str">The source text.</param>
    /// <param name="boolResult">The parsed boolean value.</param>
    /// <param name="allowLocalizedLiterals">
    /// When <see langword="false"/>, only standard <see cref="bool.TryParse(string?, out bool)"/> values are accepted.
    /// </param>
    public static bool TryToBool(string str, out bool boolResult, bool allowLocalizedLiterals = true)
    {
        boolResult = false;
        if (str.IsNullOrWhiteSpace())
        {
            return false;
        }

        var normalized = str.Trim();
        return !allowLocalizedLiterals ? bool.TryParse(normalized, out boolResult) :
            TextMappings.StringToBoolDict.TryGetValue(normalized, out boolResult);
    }

    /// <summary>
    /// Tries to parse a string into an enum, honoring <see cref="EnumAliasAttribute"/> aliases.
    /// </summary>
    public static bool TryToEnum<T>(string str, out T enumResult, bool supportNumeric = false) where T : struct, Enum
    {
        enumResult = default;
        if (TryToEnum(str, typeof(T), out var result, supportNumeric) && result is T typedResult)
        {
            enumResult = typedResult;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to parse a string into the supplied enum type, honoring <see cref="EnumAliasAttribute"/> aliases.
    /// </summary>
    public static bool TryToEnum(string str, Type enumType, out object? enumResult, bool supportNumeric = false)
    {
        enumResult = null;
        if (str.IsNullOrEmpty() || enumType is null || !enumType.IsEnum)
        {
            return false;
        }

        if (EnumAliasParser.TryParseAlias(enumType, str, out enumResult))
        {
            return true;
        }

        if (!supportNumeric && Regex.IsMatch(str, @"^[+-]?\d+$", RegexOptions.CultureInvariant))
        {
            return false;
        }

        return Enum.TryParse(enumType, str, ignoreCase: true, out enumResult);
    }

    /// <summary>
    /// Converts supported Chinese punctuation to their ASCII equivalents.
    /// </summary>
    public static string ToEnPunctuation(this string str)
    {
        if (str.IsNullOrWhiteSpace())
        {
            return str;
        }

        return str.ContainsAny(TextMappings.ZhToEnPunctuationDict.Keys)
            ? str.ReplaceBasedOnDict(TextMappings.ZhToEnPunctuationDict)
            : str;
    }

    /// <summary>
    /// Converts supported ASCII punctuation to their Chinese equivalents.
    /// </summary>
    public static string ToZhPunctuation(this string str)
    {
        if (str.IsNullOrWhiteSpace())
        {
            return str;
        }

        var result = new System.Text.StringBuilder(str.Length);
        var openingQuote = true;
        for (var index = 0; index < str.Length; index++)
        {
            if (str[index] == '"')
            {
                result.Append(openingQuote ? '“' : '”');
                openingQuote = !openingQuote;
                continue;
            }

            if (index <= str.Length - 3 && str.AsSpan(index, 3).SequenceEqual("..."))
            {
                result.Append('…');
                index += 2;
                continue;
            }

            result.Append(str[index] switch
            {
                ';' => '；',
                '.' => '。',
                ':' => '：',
                ',' => '，',
                '?' => '？',
                '!' => '！',
                '(' => '（',
                ')' => '）',
                '[' => '【',
                ']' => '】',
                '<' => '《',
                '>' => '》',
                '-' => '—',
                '$' => '￥',
                '\\' => '、',
                '~' => '～',
                _ => str[index]
            });
        }

        return result.ToString();
    }

    /// <summary>
    /// Tries to parse a textual time-span interval into a left and right bound.
    /// </summary>
    public static bool TryGetTimeSpanInterval(this string str, out TimeSpan left, out TimeSpan right)
    {
        left = default;
        right = default;
        if (str.IsNullOrWhiteSpace())
        {
            return false;
        }

        str = str.Trim();

        try
        {
            var intervalMatch = IntervalRegex.Match(str);
            if (!intervalMatch.Success ||
                !TryParseIntervalValue(intervalMatch.Groups["left"].Value, out var parsedLeft) ||
                !TryParseIntervalValue(intervalMatch.Groups["right"].Value, out var parsedRight) ||
                parsedLeft > parsedRight)
            {
                return false;
            }

            left = parsedLeft;
            right = parsedRight;
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            left = default;
            right = default;
            return false;
        }

        static bool TryParseIntervalValue(string value, out TimeSpan result)
        {
            result = default;
            var match = IntervalValueRegex.Match(value);
            if (!match.Success)
            {
                return false;
            }

            try
            {
                var days = ParseGroup("day");
                var hours = ParseGroup("hour");
                var minutes = ParseGroup("minute");
                var seconds = ParseGroup("second");
                var fraction = match.Groups["fraction"].Value;
                var fractionTicks = fraction.Length == 0
                    ? 0
                    : int.Parse(fraction.PadRight(7, '0'), CultureInfo.InvariantCulture);

                result = new TimeSpan(days, hours, minutes, seconds) + TimeSpan.FromTicks(fractionTicks);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }

            int ParseGroup(string name) => match.Groups[name].Success
                ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture)
                : 0;
        }
    }
}

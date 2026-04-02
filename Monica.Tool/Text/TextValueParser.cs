using System.Text.RegularExpressions;
using Monica.Tool.Annotations;
using Monica.Tool.Extensions;

namespace Monica.Tool.General;

/// <summary>
/// Parses common text values used by Monica modules.
/// </summary>
public static class TextValueParser
{
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

        return !allowLocalizedLiterals ? bool.TryParse(str, out boolResult) :
            TextMappings.StringToBoolDict.TryGetValue(str, out boolResult);
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
        if (str.IsNullOrEmpty())
        {
            return false;
        }

        if (EnumAliasParser.TryParseAlias(enumType, str, out enumResult))
        {
            return true;
        }

        try
        {
            if (!supportNumeric && str.IsInt())
            {
                return false;
            }

            enumResult = Enum.Parse(enumType, str, true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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

        return str.ContainsAny(TextMappings.ZhToEnPunctuationDict.Values)
            ? str.ReplaceBasedOnDict(TextMappings.ZhToEnPunctuationDict, true)
            : str;
    }

    /// <summary>
    /// Tries to parse a textual time-span interval into a left and right bound.
    /// </summary>
    public static bool TryGetTimeSpanInterval(this string str, out TimeSpan left, out TimeSpan right)
    {
        left = new TimeSpan();
        right = new TimeSpan();
        if (str.IsNullOrWhiteSpace())
        {
            return false;
        }

        str = str.Trim();

        // Matches a pair of TimeSpan-like values separated by a non-numeric delimiter.
        var regex = new Regex(@"^(?<lefttime>(?:(?:(?<lday>\d{1,7})\.)?(?:(?<lhour>2[0-3]|[0-1]\d|\d):)?(?:(?<lminute>[0-5]\d|\d):))?(?<lsecond>[0-5]\d|\d)(?:(?:\.)?(?<lmillisecond>\d{1,7}))?)[^.:\d]+?(?<righttime>(?:(?:(?<rday>[0-5]\d|\d)\.)?(?:(?<rhour>2[0-3]|[0-1]\d|\d):)?(?:(?<rminute>[0-5]\d|\d):))?(?<rsecond>[0-5]\d|\d))(?:(?:\.)?(?<rmillisecond>\d{1,7}))?$");
        if (regex.IsMatch(str))
        {
            var groups = regex.Match(str).Groups;
            int.TryParse(groups["lday"]?.Value, out var lday);
            int.TryParse(groups["lhour"]?.Value, out var lhour);
            int.TryParse(groups["lminute"]?.Value, out var lminute);
            int.TryParse(groups["lsecond"]?.Value, out var lsecond);
            int.TryParse(groups["lmillisecond"]?.Value, out var lmillisecond);
            int.TryParse(groups["rday"]?.Value, out var rday);
            int.TryParse(groups["rhour"]?.Value, out var rhour);
            int.TryParse(groups["rminute"]?.Value, out var rminute);
            int.TryParse(groups["rsecond"]?.Value, out var rsecond);
            int.TryParse(groups["rmillisecond"]?.Value, out var rmillisecond);
            left = new TimeSpan(lday, lhour, lminute, lsecond, lmillisecond);
            right = new TimeSpan(rday, rhour, rminute, rsecond, rmillisecond);
            if (left <= right)
            {
                return true;
            }
        }

        return false;
    }
}

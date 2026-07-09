using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Monica.Tool.Text;

namespace Monica.Tool.Extensions;

[Flags]
public enum UnicodeRegexs
{
    /// <summary>
    /// \u4e00-\u9fa5
    /// </summary>
    Chinese = 1 << 0,
    /// <summary>
    /// A-Z
    /// </summary>
    UpperLetter= 1 << 1,
    /// <summary>
    /// a-z
    /// </summary>
    LowerLetter= 1 << 2,
    /// <summary>
    /// 0-9
    /// </summary>
    Digit= 1 << 3,
    /// <summary>
    /// ，。！？：
    /// </summary>
    ChineseCommonPunctuationMarks = 1 << 4,
    /// <summary>
    /// ,.?!:
    /// </summary>
    EnglishCommonPunctuationMarks = 1 << 5,
    /// <summary>
    /// A-Za-z
    /// </summary>
    Letter = LowerLetter|UpperLetter,
    ValidCharacters = Digit|Letter|Chinese
}
/// <summary>
/// Extension Methods of Regex related
/// </summary>
public static class RegexExtensions
{
    public static string Get(this UnicodeRegexs regexs)
    {
        var sb = new StringBuilder();
        if (regexs.HasFlag(UnicodeRegexs.UpperLetter)) sb.Append("A-Z");
        if (regexs.HasFlag(UnicodeRegexs.LowerLetter)) sb.Append("a-z");
        if (regexs.HasFlag(UnicodeRegexs.Digit)) sb.Append("0-9");
        if(regexs.HasFlag(UnicodeRegexs.Chinese)) sb.Append("\u4e00-\u9fa5".ToRegexUnicodeEscaped());
        if(regexs.HasFlag(UnicodeRegexs.ChineseCommonPunctuationMarks)) sb.Append("，。！？：");
        if(regexs.HasFlag(UnicodeRegexs.EnglishCommonPunctuationMarks)) sb.Append(",.?!:");
        return sb.ToString();
    }

    /// <summary>
    /// Indicates whether the specified regular expression finds a match in the specified input string, using the specified matching options.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="pattern">The regular expression pattern to match.</param>
    /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
    /// <returns><see langword="true" /> if the regular expression finds a match; otherwise, <see langword="false" />.</returns>
    public static bool IsMatch(this string? input, [RegexPattern] string pattern, RegexOptions options = RegexOptions.None)
    {
        return input != null && Regex.IsMatch(input, pattern, options);
    }

    /// <summary>
    /// Search for the first match of the specified regular expression and get the captured substring.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="pattern">The regular expression pattern to match.</param>
    /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
    /// <returns>Not matched return <see cref="string.Empty"/>.</returns>
    public static string Match(this string? input, [RegexPattern] string pattern, RegexOptions options = RegexOptions.None)
    {
        if (input == null) return "";
        var result = Regex.Match(input, pattern, options);
        return result.Success ? result.Value : "";
    }
    /// <summary>
    /// Search for the first match of the specified regular expression and get the captured match group.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="pattern">The regular expression pattern to match.</param>
    /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
    /// <param name="groupResult">The result of the matched group.</param>
    /// <returns>Not matched return <see langword="false"/>.</returns>
    public static bool MatchOnce(this string input,
        [RegexPattern] string pattern,
        [NotNullWhen(true)]out GroupCollection? groupResult,
        RegexOptions options = RegexOptions.None)
    {
        groupResult = null;
        var regex = new Regex(pattern, options);
        var result = regex.Match(input);
        if (!result.Success) return false;
        groupResult = result.Groups;
        return true;
    }
    /// <summary>
    /// Search for the first match of the specified regular expression and get the captured match group, and get the replaced string.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="pattern">The regular expression pattern to match.</param>
    /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
    /// <param name="replaced">Replaced string, or return the original string if it does not match</param>
    /// <param name="groupResult">The result of the matched group.</param>
    /// <param name="replaceTo"></param>
    /// <returns>Not matched return <see langword="false"/>.</returns>
    public static bool MatchOnceThenReplace(this string input,
        [RegexPattern] string pattern, out string replaced,
        [NotNullWhen(true)]out GroupCollection? groupResult,
        RegexOptions options = RegexOptions.None, string replaceTo = "")
    {
        replaced = input;
        groupResult = null;
        var regex = new Regex(pattern, options);
        var result = regex.Match(input);
        if (!result.Success) return false;
        groupResult = result.Groups;
        replaced = regex.Replace(input, replaceTo, 1);
        return true;
    }
       
    /// <summary>
    /// Searches the specified input string for all occurrences of a specified regular expression, using the specified matching options.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="pattern">The regular expression pattern to match.</param>
    /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
    /// <returns>If not matched ones will return empty list.</returns>
    public static List<string> Matches(this string? input, [RegexPattern] string pattern, RegexOptions options = RegexOptions.None)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(input)) return list;
        list.AddRange(from Match item in Regex.Matches(input, pattern, options) select item.Value);
        return list;
    }

    /// <summary>
    /// In a specified input string, replaces all strings that match a specified regular expression with a string returned by a <see cref="MatchEvaluator" /> delegate.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="pattern">The regular expression pattern to match.</param>
    /// <param name="evaluator">A custom method that examines each match and returns either the original matched string or a replacement string.</param>
    /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
    /// <param name="count">The maximum number of times the replacement can occur. if zero replace all.</param>
    /// <returns></returns>
    public static string RegexReplace(this string input, [RegexPattern] string pattern, MatchEvaluator evaluator,
        RegexOptions options = RegexOptions.None, int count = 0)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var regex = new Regex(pattern, options);
        return count > 0 ? regex.Replace(input, evaluator, count) : regex.Replace(input, evaluator);
    }
    /// <summary>
    /// In a specified input string, replaces a specified maximum number of strings that match a regular expression pattern with a specified replacement string.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="pattern">The regular expression pattern to match.</param>
    /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
    /// <param name="replacement">The replacement string.</param>
    /// <param name="count">The maximum number of times the replacement can occur. if zero replace all.</param>
    /// <returns></returns>
    public static string RegexReplace(this string input, [RegexPattern] string pattern, string replacement = "",
        RegexOptions options = RegexOptions.None, int count = 0)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var regex = new Regex(pattern, options);
        return count > 0 ? regex.Replace(input, replacement, count) : regex.Replace(input, replacement);
    }
    /// <summary>
    /// Finds all occurrences of the given named capturing group name in a string that match the regular expression. A List returning Count=0 was not found.
    /// <br/>English: Finds all the matches in a string that match the regular expression pattern and the given named capture group name. Not found returns a List with Count=0.
    /// </summary>
    /// <param name="s">the string to test</param>
    /// <param name="pattern">Regular expression pattern to match</param>
    /// <param name="groupName">Names in named capture groups in regular expressions</param>
    /// <param name="regexOptions">Match using specified options, which can be combined bitwise</param>
    /// <returns></returns>
    public static List<string> MatchedGroupValues(this string? s,
        [RegexPattern] string pattern, string groupName,
        RegexOptions regexOptions = RegexOptions.None)
    {
        var itemList = new List<string>();
        if (s == null || groupName.IsNullOrEmpty()) return itemList;
        var regex = new Regex(pattern, regexOptions);
        foreach (Match match in regex.Matches(s))
        {
            var matched = match.Groups[groupName].Value;
            if (matched != "") itemList.Add(matched);
        }

        return itemList;
    }

    /// <summary>
    /// Determine whether it is a valid regular expression pattern.
    /// <br/>English: Determine whether it is a valid regular expression pattern.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="error">If it is an invalid regular expression, the error reason is output.</param>
    /// <returns></returns>
    public static bool IsValidRegexPattern(this string? s, out string error)
    {
        error = "";
        if (s == null)
        {
            error = "pattern is empty!";
            return false;
        }
        try
        {
            var regex = new Regex(s);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}

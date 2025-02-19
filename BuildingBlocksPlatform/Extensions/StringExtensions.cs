using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace BuildingBlocksPlatform.Extensions
{
    public static class StringExtensions
    {
        public static readonly string[] DATE_FORMATS = ["yyyy-MM-dd", "yyyyMMdd", "MMdd"];
        public static readonly string[] TIME_FORMATS = ["HHmm", "HH:mm"];

        public static DateOnly? ToDateOnly(this string dateStr)
        {
            if (DateOnly.TryParseExact(dateStr, DATE_FORMATS, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var outDateOnly))
            {
                return outDateOnly;
            }

            return null;
        }

        public static TimeOnly? ToTimeOnly(this string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr))
            {
                return null;
            }

            if (TimeOnly.TryParseExact(timeStr, TIME_FORMATS, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var outTime))
            {
                return outTime;
            }

            return null;
        }

        public static DateTime? ToDateTime(this string dateStr)
        {
            if (DateTime.TryParseExact(dateStr, DATE_FORMATS, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var outDateOnly))
            {
                return outDateOnly;
            }

            return null;
        }

        public static string BeIfNullOrEmpty(this string? obj, params string[] nextValues)
        {
            if (!string.IsNullOrEmpty(obj))
            {
                return obj;
            }

            foreach (var value in nextValues)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }


        /// <summary>
        /// 根据标签拆分，返回标签键值对。
        /// 例如： text: FLIGHT ROUTE:ZLXY-ZSNB-01\r\nREMARK:RMK   label: [FLIGHT ROUTE,REMARK]
        /// 返回列表    [("FLIGHT ROUTE", "ZLXY-ZSNB-01"),("REMARK","RMK)]
        /// </summary>
        /// <param name="text"></param>
        /// <param name="labels"></param>
        /// <returns></returns>
        public static List<(string, string)> SplitByLabel(this string text, string[] labels)
        {
            var key = "";
            var value = "";
            List<(string, string)> result = new();

            foreach (var line in text.Split(Environment.NewLine,StringSplitOptions.TrimEntries))
            {
                var labelFound = labels.FirstOrDefault(s => line.Contains(s));

                if (labelFound != null) // 找到标签
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        result.Add((key, value));
                    }

                    key = labelFound;
                    value = line[(line.IndexOf(':') + 1)..];
                }
                else if (!string.IsNullOrEmpty(line))
                {
                    value += Environment.NewLine + line;
                }
            }

            if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(key))
            {
                result.Add((key, value));
            }

            return result;
        }

        /// <summary>
        /// 根据标签的正则拆分，返回标签键值对。
        /// 例如： text: AAAA-AAAA:AAAA-AAAA-01\r\BBBB-BBBB:BBBB-BBBB-01\r\CCCC-CCCC:CCCC-CCCC-01, pattern: [A-Z]{4}-[A-Z]{4}:
        /// 返回列表    [("AAAA-AAAA", "AAAA-AAAA-01"),("BBBB-BBBB", "BBBB-BBBB-01"),("CCCC-CCCC", "CCCC-CCCC-01")]
        /// </summary>
        /// <param name="text"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static List<(string, string)> SplitByLabel(this string text, string pattern)
        {
            var key = "";
            var value = "";
            List<(string, string)> result = new();

            foreach (var line in text.Split(Environment.NewLine, StringSplitOptions.TrimEntries))
            {
                var labelMatch = Regex.Match(line, pattern);

                if (labelMatch.Success) // 找到标签
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        result.Add((key, value));
                    }

                    key = labelMatch.Value;
                    value = line[(line.IndexOf(key) + key.Length)..];
                }
                else if (!string.IsNullOrEmpty(line))
                {
                    value += Environment.NewLine + line;
                }
            }

            if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(key))
            {
                result.Add((key, value));
            }

            return result;
        }

        public static string NextWord(this string text, string[] labels, string pattern = "")
        {
            var index = -1;
            var foundLabel = "";
            foreach (var label in labels)
            {
                index = text.IndexOf(label, StringComparison.InvariantCulture);
                if (index != -1)
                {
                    foundLabel = label;
                    break;
                }
            }

            if (index == -1) return "";

            string retValue;
            var startPos = index + foundLabel.Length;
            var endPos = text.IndexOf(' ', startPos);

            if (endPos == -1) retValue = text[startPos..];
            else
            {
                retValue = text.Substring(startPos, endPos - startPos);
            }

            if (string.IsNullOrEmpty(retValue)) return "";

            if (!string.IsNullOrEmpty(pattern))
            {
                var match = Regex.Match(retValue, $"^{pattern}$" );
                if (match.Success)
                {
                    return retValue;
                }
                else
                {
                    return "";
                }
            }

            return retValue;
        }

        #region ABP移植
        /// <summary>
        /// Concatenates the members of a constructed <see cref="IEnumerable{T}"/> collection of type System.String, using the specified separator between each member.
        /// This is a shortcut for string.Join(...)
        /// </summary>
        /// <param name="source">A collection that contains the strings to concatenate.</param>
        /// <param name="separator">The string to use as a separator. separator is included in the returned string only if values has more than one element.</param>
        /// <returns>A string that consists of the members of values delimited by the separator string. If values has no members, the method returns System.String.Empty.</returns>
        public static string JoinAsString(this IEnumerable<string> source, string separator)
        {
            return string.Join(separator, source);
        }

        /// <summary>
        /// Concatenates the members of a collection, using the specified separator between each member.
        /// This is a shortcut for string.Join(...)
        /// </summary>
        /// <param name="source">A collection that contains the objects to concatenate.</param>
        /// <param name="separator">The string to use as a separator. separator is included in the returned string only if values has more than one element.</param>
        /// <typeparam name="T">The type of the members of values.</typeparam>
        /// <returns>A string that consists of the members of values delimited by the separator string. If values has no members, the method returns System.String.Empty.</returns>
        public static string JoinAsString<T>(this IEnumerable<T> source, string separator)
        {
            return string.Join(separator, source);
        }
        /// <summary>
        /// Removes first occurrence of the given postfixes from end of the given string.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="postFixes">one or more postfix.</param>
        /// <returns>Modified string or the same string if it has not any of given postfixes</returns>
        [ContractAnnotation("null <= str:null")]
        public static string RemovePostFix(this string str, params string[] postFixes)
        {
            return str.RemovePostFix(StringComparison.Ordinal, postFixes);
        }

        /// <summary>
        /// Removes first occurrence of the given postfixes from end of the given string.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="comparisonType">String comparison type</param>
        /// <param name="postFixes">one or more postfix.</param>
        /// <returns>Modified string or the same string if it has not any of given postfixes</returns>
        [ContractAnnotation("null <= str:null")]
        public static string RemovePostFix(this string str, StringComparison comparisonType, params string[] postFixes)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            if (postFixes.Length <= 0)
            {
                return str;
            }


            foreach (var postFix in postFixes)
            {
                if (str.EndsWith(postFix, comparisonType))
                {
                    return str[..^postFix.Length];
                }
            }

            return str;
        }

        /// <summary>
        /// Removes first occurrence of the given prefixes from beginning of the given string.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="preFixes">one or more prefix.</param>
        /// <returns>Modified string or the same string if it has not any of given prefixes</returns>
        [ContractAnnotation("null <= str:null")]
        public static string RemovePreFix(this string str, params string[] preFixes)
        {
            return str.RemovePreFix(StringComparison.Ordinal, preFixes);
        }

        /// <summary>
        /// Removes first occurrence of the given prefixes from beginning of the given string.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="comparisonType">String comparison type</param>
        /// <param name="preFixes">one or more prefix.</param>
        /// <returns>Modified string or the same string if it has not any of given prefixes</returns>
        [ContractAnnotation("null <= str:null")]
        public static string RemovePreFix(this string str, StringComparison comparisonType, params string[] preFixes)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            if (preFixes.Length <= 0)
            {
                return str;
            }

            foreach (var preFix in preFixes)
            {
                if (str.StartsWith(preFix, comparisonType))
                {
                    return str[preFix.Length..];
                }
            }

            return str;
        }

        public static string ReplaceFirst(this string str, string search, string replace, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var pos = str.IndexOf(search, comparisonType);
            if (pos < 0)
            {
                return str;
            }

            var searchLength = search.Length;
            var replaceLength = replace.Length;
            var newLength = str.Length - searchLength + replaceLength;

            var buffer = newLength <= 1024 ? stackalloc char[newLength] : new char[newLength];

            // Copy the part of the original string before the search term
            str.AsSpan(0, pos).CopyTo(buffer);

            // Copy the replacement text
            replace.AsSpan().CopyTo(buffer[pos..]);

            // Copy the remainder of the original string
            str.AsSpan(pos + searchLength).CopyTo(buffer[(pos + replaceLength)..]);

            return buffer.ToString();
        }

        /// <summary>
        /// Gets a substring of a string from beginning of the string.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="str"/> is null</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="len"/> is bigger that string's length</exception>
        public static string Left(this string str, int len)
        {
            if (str.Length < len)
            {
                throw new ArgumentException("len argument can not be bigger than given string's length!");
            }

            return str.Substring(0, len);
        }

        /// <summary>
        /// Gets a substring of a string from end of the string.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="str"/> is null</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="len"/> is bigger that string's length</exception>
        public static string Right(this string str, int len)
        {
            if (str.Length < len)
            {
                throw new ArgumentException("len argument can not be bigger than given string's length!");
            }

            return str.Substring(str.Length - len, len);
        }
        /// <summary>
        /// Converts PascalCase string to camelCase string.
        /// </summary>
        /// <param name="str">String to convert</param>
        /// <param name="useCurrentCulture">set true to use current culture. Otherwise, invariant culture will be used.</param>
        /// <param name="handleAbbreviations">set true to if you want to convert 'XYZ' to 'xyz'.</param>
        /// <returns>camelCase of the string</returns>
        [ContractAnnotation("null <= str:null")]
        public static string ToCamelCase(this string str, bool useCurrentCulture = false, bool handleAbbreviations = false)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            if (str.Length == 1)
            {
                return useCurrentCulture ? str.ToLower() : str.ToLowerInvariant();
            }

            if (handleAbbreviations && IsAllUpperCase(str))
            {
                return useCurrentCulture ? str.ToLower() : str.ToLowerInvariant();
            }

            return (useCurrentCulture ? char.ToLower(str[0]) : char.ToLowerInvariant(str[0])) + str[1..];
        }
        private static bool IsAllUpperCase(string input)
        {
            return input.All(t => !char.IsLetter(t) || char.IsUpper(t));
        }
        /// <summary>
        /// Converts given PascalCase/camelCase string to sentence (by splitting words by space).
        /// Example: "ThisIsSampleSentence" is converted to "This is a sample sentence".
        /// </summary>
        /// <param name="str">String to convert.</param>
        /// <param name="useCurrentCulture">set true to use current culture. Otherwise, invariant culture will be used.</param>
        [ContractAnnotation("null <= str:null")]
        public static string ToSentenceCase(this string str, bool useCurrentCulture = false)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            return useCurrentCulture
                ? Regex.Replace(str, "[a-z][A-Z]", m => m.Value[0] + " " + char.ToLower(m.Value[1]))
                : Regex.Replace(str, "[a-z][A-Z]", m => m.Value[0] + " " + char.ToLowerInvariant(m.Value[1]));
        }

        /// <summary>
        /// Converts given PascalCase/camelCase string to kebab-case.
        /// </summary>
        /// <param name="str">String to convert.</param>
        /// <param name="useCurrentCulture">set true to use current culture. Otherwise, invariant culture will be used.</param>
        [ContractAnnotation("null <= str:null")]
        public static string ToKebabCase(this string str, bool useCurrentCulture = false)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            str = str.ToCamelCase();

            return useCurrentCulture
                ? Regex.Replace(str, "[a-z][A-Z]", m => m.Value[0] + "-" + char.ToLower(m.Value[1]))
                : Regex.Replace(str, "[a-z][A-Z]", m => m.Value[0] + "-" + char.ToLowerInvariant(m.Value[1]));
        }

        /// <summary>
        /// Converts given PascalCase/camelCase string to snake case.
        /// Example: "ThisIsSampleSentence" is converted to "this_is_a_sample_sentence".
        /// https://github.com/npgsql/npgsql/blob/dev/src/Npgsql/NameTranslation/NpgsqlSnakeCaseNameTranslator.cs#L51
        /// </summary>
        /// <param name="str">String to convert.</param>
        /// <returns></returns>
        public static string ToSnakeCase(this string str)
        {
            return string.IsNullOrWhiteSpace(str) ? str : JsonNamingPolicy.SnakeCaseLower.ConvertName(str);
        }
        /// <summary>
        /// Converts camelCase string to PascalCase string.
        /// </summary>
        /// <param name="str">String to convert</param>
        /// <param name="useCurrentCulture">set true to use current culture. Otherwise, invariant culture will be used.</param>
        /// <returns>PascalCase of the string</returns>
        [ContractAnnotation("null <= str:null")]
        public static string ToPascalCase(this string str, bool useCurrentCulture = false)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            if (str.Length == 1)
            {
                return useCurrentCulture ? str.ToUpper() : str.ToUpperInvariant();
            }

            return (useCurrentCulture ? char.ToUpper(str[0]) : char.ToUpperInvariant(str[0])) + str[1..];
        }

        #endregion
    }
}

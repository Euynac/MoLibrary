using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using MoLibrary.Tool.General;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of string
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Convert string to byte array use specific encoding.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="encoding">default is <see cref="Encoding"/>.UTF8</param>
        /// <returns></returns>
        public static byte[] ConvertToBytes(this string str, Encoding? encoding = null)
        {
            return (encoding ?? Encoding.UTF8).GetBytes(str);
        }
        /// <summary>
        /// Convert byte array to string use specific encoding.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="encoding">default is <see cref="Encoding"/>.UTF8</param>
        /// <returns></returns>
        public static string ConvertToString(this byte[] bytes, Encoding? encoding = null) =>
            (encoding ?? Encoding.UTF8).GetString(bytes);
        /// <summary>
        /// Convert string to stream use specific encoding.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="encoding">default is <see cref="Encoding"/>.UTF8</param>
        /// <returns></returns>
        public static Stream ConvertToStream(this string str, Encoding? encoding = null)
        {
            var byteArray = (encoding ?? Encoding.UTF8).GetBytes(str);
            return new MemoryStream(byteArray);
        }
        /// <summary>
        /// Convert stream to string.
        /// </summary>
        /// <returns></returns>
        public static string ConvertToString(this Stream stream)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Split camel case type string into string array. eg: CamelCase => Camel Case.
        /// <para>other character such as Chinese will be discarded.</para>
        /// </summary>
        /// https://stackoverflow.com/a/37532157/18731746
        /// <param name="str"></param>
        /// <param name="supportOtherIdentifier">supports any identifier with words, acronyms, numbers, underscores. Default will discard these character.</param>
        /// <returns></returns>
        public static string[] CamelCaseSplit(this string str, bool supportOtherIdentifier = false)
        {
            var regex = supportOtherIdentifier ? "([A-Z]+(?![a-z])|[A-Z][a-z]+|[0-9]+|[a-z]+)" : @"(^[a-z]+|[A-Z]+(?![a-z])|[A-Z][a-z]+)";
            return Regex.Matches(str, regex).Select(m => m.Value).ToArray();
        } 

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.String" /> class to the value indicated by a specified Unicode character repeated a specified number of times.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="times"></param>
        /// <returns></returns>
        public static string Repeat(this char c, int times) => new(c, times);
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.String" /> class to the value indicated by a specified string repeated a specified number of times.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="times"></param>
        /// <returns></returns>
        public static string Repeat(this string str, int times) => new(Enumerable.Range(0, times).SelectMany(x => str).ToArray());
        /// <summary>
        /// Indicates whether the specified string is NOT null or an <see cref="F:System.String.Empty"></see> string.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns>true if the <paramref name="value">value</paramref> parameter is null or an empty string (""); otherwise, false.</returns>
        [ContractAnnotation("null => false")]
        public static bool IsNotNullOrEmpty([NotNullWhen(false)] this string? value) => !string.IsNullOrEmpty(value);

        /// <summary>
        /// Indicates whether a specified string is NOT null, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns>true if the <paramref name="value">value</paramref> parameter is null or <see cref="F:System.String.Empty"></see>, or if <paramref name="value">value</paramref> consists exclusively of white-space characters.</returns>
        [ContractAnnotation("null => false")]
        public static bool IsNotNullOrWhiteSpace([NotNullWhen(false)] this string? value) => !string.IsNullOrWhiteSpace(value);
        
        /// <summary>
        /// Indicates whether the specified string is null or an <see cref="F:System.String.Empty"></see> string.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns>true if the <paramref name="value">value</paramref> parameter is null or an empty string (""); otherwise, false.</returns>
        [ContractAnnotation("null => true")] //能够教会ReSharper空判断(传入的是null，返回true)https://www.jetbrains.com/help/resharper/Contract_Annotations.html#syntax
        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value) => string.IsNullOrEmpty(value);

        /// <summary>
        /// Indicates whether a specified string is null, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns>true if the <paramref name="value">value</paramref> parameter is null or <see cref="F:System.String.Empty"></see>, or if <paramref name="value">value</paramref> consists exclusively of white-space characters.</returns>
        [ContractAnnotation("null => true")]
        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value) => string.IsNullOrWhiteSpace(value);
        /// <summary>
        /// Trim specific string once at the end of given string.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="strToTrim"></param>
        /// <returns></returns>
        [ContractAnnotation("str:notnull => notnull; str:null => null")]
        public static string? TrimEndOnce(this string? str, string strToTrim)
        {
            if (str.IsNullOrEmpty() || strToTrim.IsNullOrEmpty()) return str;
            return !str.EndsWith(strToTrim) ? str : str[..^strToTrim.Length];
        }

        /// <summary>
        /// 判断是否能够被转换为int型
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsInt(this string? s) => s != null && int.TryParse(s, out _);

        /// <summary>
        /// 判断是否能够被转换为int型
        /// </summary>
        /// <param name="s"></param>
        /// <param name="num">成功返回转换的int</param>
        /// <returns></returns>
        public static bool IsInt(this string? s, out int num)
        {
            num = default;
            return s != null && int.TryParse(s, out num);
        }


        /// <summary>
        /// 将某段字符串按照某指定字符串分割成数组后，查找一个字符串是否在数组中
        /// </summary>
        /// <param name="source">要分割的字符串</param>
        /// <param name="comparisonType">指定搜索规则的枚举值之一</param>
        /// <param name="searchStr">要查找的字符串</param>
        /// <param name="splitArray">分割字符串，输入多个就将其都作为分割符</param>
        /// <returns></returns>
        public static bool SplitContain(this string? source, StringComparison comparisonType, string searchStr,
            params string[] splitArray)
        {
            return !string.IsNullOrEmpty(source) && source.Split(splitArray, StringSplitOptions.RemoveEmptyEntries).Any(item => item.Equals(searchStr, comparisonType));
        }
        /// <summary>
        /// 将某段字符串按照某指定字符分割成数组后，查找一个字符串是否在数组中
        /// </summary>
        /// <param name="source">要分割的字符串</param>
        /// <param name="comparisonType">指定搜索规则的枚举值之一</param>
        /// <param name="searchStr">要查找的字符串</param>
        /// <param name="splitArray">分割字符，输入多个就将其都作为分割符</param>
        /// <returns></returns>
        public static bool SplitContain(this string? source, StringComparison comparisonType, string searchStr,
            params char[] splitArray)
        {
            if (string.IsNullOrEmpty(source)) return false;
            return source.Split(splitArray, StringSplitOptions.RemoveEmptyEntries).Any(item => item.Equals(searchStr, comparisonType));
        }
        /// <summary>
        /// 指定字符串在某字符串中出现的所有Index集合（可用于判断出现次数），没有则返回count=0的list
        /// </summary>
        /// <param name="source">源字符串</param>
        /// <param name="value">要搜索的字符串</param>
        /// <param name="repeat">可以重复判断已经出现过的字符（即包括子序列）</param>
        /// <returns></returns>
        public static List<int> AllIndexOf(this string? source, string value, bool repeat = false)
        {
            var list = new List<int>();
            if (source.IsNullOrEmpty()) return list;
            var i = 0;
            while (i >= 0 && i < source.Length)
            {
                i = source.IndexOf(value, i, StringComparison.Ordinal);
                if (i < 0) break;
                list.Add(i);
                if (repeat) i++;
                else i += value.Length;
            }
            return list;
        }
        /// <summary>
        /// 返回一个值，该值指示指定的子串是否出现在此字符串中。
        /// </summary>
        /// <param name="source">源字符串</param>
        /// <param name="value">要搜寻的字符串。</param>
        /// <param name="comparisonType">指定搜索规则的枚举值之一</param>
        /// <returns>如果 true 参数出现在此字符串中，或者 value 为空字符串 ("")，则为 value；否则为 false。</returns>
        public static bool Contains(this string source, string value, StringComparison comparisonType) => source.IndexOf(value, comparisonType) >= 0;

        /// <summary>
        /// Determine whether any of the strings in specified set is in source string.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="strSet">specified set which contains strings wanted to determine</param>
        /// <param name="comparisonType">specified comparision rule</param>
        /// <returns></returns>
        public static bool ContainsAny(this string source, IEnumerable<string> strSet, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(source) || strSet.IsNullOrEmptySet()) return false;
            return strSet.Any(s => source.Contains(s, comparisonType));
        }

        /// <summary>
        /// Determine whether any of the specified strings is in source string.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="testStrings"></param>
        /// <returns></returns>
        public static bool ContainsAny(this string source, params string[] testStrings) =>
            ContainsAny(source, false, out _, testStrings);
        /// <summary>
        /// Determine whether any of the specified strings is in source string.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="testStrings"></param>
        /// <param name="culprit">the string contained in source string</param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        public static bool ContainsAny(this string source, bool ignoreCase, out string? culprit, params string[] testStrings) => InnerWithAny(2, source, ignoreCase, out culprit, testStrings);

        /// <summary>
        /// Determine whether source string starts with any of the specified strings.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="testStrings"></param>
        /// <returns></returns>
        public static bool StartsWithAny(this string source, params string[] testStrings) => StartsWithAny(source, false, out _, testStrings);
        /// <summary>
        /// Determine whether source string starts with any of the specified strings.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="ignoreCase"></param>
        /// <param name="culprit">the string which the source string starts with.</param>
        /// <param name="testStrings"></param>
        /// <returns></returns>
        public static bool StartsWithAny(this string source, bool ignoreCase, out string? culprit, params string[] testStrings) => InnerWithAny(1, source, ignoreCase, out culprit, testStrings);
        /// <summary>
        /// Determine whether source string ends with any of the specified strings.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="testStrings"></param>
        /// <returns></returns>
        public static bool EndsWithAny(this string source, params string[] testStrings) => EndsWithAny(source, false, out _, testStrings);
        /// <summary>
        /// Determine whether source string ends with any of the specified strings.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="ignoreCase"></param>
        /// <param name="culprit">the string which the source string ends with.</param>
        /// <param name="testStrings"></param>
        /// <returns></returns>
        public static bool EndsWithAny(this string source, bool ignoreCase, out string? culprit, params string[] testStrings) => InnerWithAny(1, source, ignoreCase, out culprit, testStrings);
        private static bool InnerWithAny(int kind, string source, bool ignoreCase, out string? culprit, params string[] testStrings)
        {
            var type = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var s in testStrings)
            {
                switch (kind)
                {
                    case 1:
                    {
                        if (source.StartsWith(s, type))
                        {
                            culprit = s;
                            return true;
                        }

                        continue;
                    }
                    case 2:
                    {
                        if (source.Contains(s, type))
                        {
                            culprit = s;
                            return true;
                        }
                        continue;
                    }
                    case 3:
                    {
                        if (source.EndsWith(s, type))
                        {
                            culprit = s;
                            return true;
                        }
                        continue;
                    }
                    default:
                        throw new Exception("not supported.");
                }
                
            }

            culprit = null;
            return false;
        }

        /// <summary>
        /// Filter specific chars from given string.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="charsToRemove"></param>
        /// <returns></returns>
        public static string FilterChars(this string source, params char[] charsToRemove)
        {
            var regex = new Regex($"[{new string(charsToRemove)}]");
            return regex.Replace(source, "");
        }
        /// <summary>
        /// Filter '\', '/', ':', '*', '?', '"', '&lt;', '&gt;', '|','.' chars from given string.
        /// </summary>
        /// <param name="source"></param>
        public static string FilterForFileName(this string source) => 
            source.FilterChars('\\', '/', ':', '*', '?', '"', '<', '>', '|','.');

        /// <summary>
        /// Limit string max length to given length.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="maxLength">length big than 0</param>
        /// <param name="appendEnd">if exceed max length, this part will append to trimmed string end.</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <returns>if exceed max length will cut of to match max length.</returns>
        public static string LimitMaxLength(this string str, int maxLength, string appendEnd)
        {
            if (maxLength <= 0) throw new InvalidOperationException($"{nameof(maxLength)} must big than 0");
            return str.Length > maxLength ? str[..maxLength] + appendEnd : str;
        }
        /// <summary>
        /// Limit string max length to given length.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="maxLength">length big than 0</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <returns>if exceed max length will cut of to match max length.</returns>
        public static string LimitMaxLength(this string str, int maxLength)
        {
            if (maxLength <= 0) throw new InvalidOperationException($"{nameof(maxLength)} must big than 0");
            return str.Length > maxLength ? str[..maxLength] : str;
        }

        /// <summary>
        /// Limit string min length to given length.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="minLength">length big than 0</param>
        /// <param name="paddingChar">default append blank space.</param>
        /// <param name="padStart">default is append to end.</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <returns>if not enough will append with blank space or given char.</returns>
        public static string LimitMinLength(this string str, int minLength, char paddingChar = ' ', bool padStart = false)
        {
            if (minLength <= 0) throw new InvalidOperationException($"{nameof(minLength)} must big than 0");
            return str.Length > minLength ? str :
                padStart ? str.PadLeft(minLength, paddingChar) : str.PadRight(minLength, paddingChar);
        }

        /// <summary>
        /// Limit string length exact to given length, 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="length">the return string's length.</param>
        /// <param name="paddingChar">default append blank space.</param>
        /// /// <param name="padStart">default is append to end.</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <returns>if not enough will append with blank space or given char, else if exceed length will cut of to match max length.</returns>
        public static string LimitLengthTo(this string str, int length, char paddingChar = ' ', bool padStart = false)
        {
            if (length <= 0) throw new InvalidOperationException($"{nameof(length)} must big than 0");
            return str.LimitMaxLength(length).LimitMinLength(length, paddingChar, padStart);
        }
        /// <summary>
        /// 将一个string中与提供的键值对集合中的键相同的全部替换为对应的值（注意若是提供的集合中值与键相包含会造成重复替换）
        /// </summary>
        /// <param name="source">原文</param>
        /// <param name="pairList">提供的键值对集合</param>
        /// <param name="reverse">默认正向Key替换为Value，为true则Value替换Key</param>
        /// <returns></returns>
        /// https://github.com/Caballero77/FlashText.NET
        public static string ReplaceBasedOnDict(this string source, IEnumerable<KeyValuePair<string, string>> pairList, bool reverse = false)
        {
            var stringBuilder = new StringBuilder(source);
            foreach (var (key, value) in pairList)
            {
                if (reverse) stringBuilder.Replace(value, key);
                else stringBuilder.Replace(key, value);
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Render given string to title case. (Capitalize each first letter of a word)
        /// </summary>
        /// <param name="input"></param>
        /// <param name="cultureInfo">Default is use en-US culture.</param>
        /// <returns></returns>
        public static string ToTitleCase(this string input, CultureInfo? cultureInfo = null)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var info = cultureInfo?.TextInfo ??
                       new CultureInfo("en-US", false).TextInfo;
            return info.ToTitleCase(input);
        }

   

        /// <summary>
        /// Get stable string hash code that will not effect by new runtime.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static int GetStableHashCode(this string str)
        {
            unchecked
            {
                var hash1 = 5381;
                var hash2 = hash1;

                for (var i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
        /// <summary>
        /// ToUpper the first char of string.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToUpperFirst(this string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            return string.Create(s.Length, s, (chars, state) =>
            {
                state.AsSpan().CopyTo(chars);
                chars[0] = char.ToUpper(chars[0]);
            });
        }
        /// <summary>
        /// Returns the input string with the first character converted to uppercase
        /// </summary>
        public static string FirstCharToUpper(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var firstCharacter = char.ToUpperInvariant(input[0]).ToString();
            return string.Concat(firstCharacter, input.Substring(1, input.Length - 1));
        }

        private const string SpecialCharacters = "!@#$%^&*()-+=/\\{}[]|:;\"'<>,.?~~`_——！—？·：；、。《》（）￥…【】”“‘’\n\t\r ";
        private static readonly string _regexSpecialCharacters = SpecialCharacters.ToRegexEscapedAll();
        private static readonly char[] _regexSpecialChars = SpecialCharacters.ToCharArray();
        public static string RemoveSpecialCharacters(this string input)
        {
            return input.RegexReplace($"[{_regexSpecialCharacters}]");
        }

        public static string TrimSpecialCharacters(this string input)
        {
            return input.Trim(_regexSpecialChars);
        }

        public static string RemoveOtherCharacters(this string input, UnicodeRegexs supportCharacters)
        {
            return input.RegexReplace($"[^{supportCharacters.Get()}]");
        }
        public static string TrimOtherCharacters(this string input, UnicodeRegexs supportCharacters)
        {
            return input.RegexReplace($"^[^{supportCharacters.Get()}]+").RegexReplace($"[^{supportCharacters.Get()}]+$");
        }
    }
}
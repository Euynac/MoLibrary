using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MoLibrary.Tool.Algorithm;
using MoLibrary.Tool.Maths;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// 通用字符串工具类
    /// </summary>
    public static class StringTool
    {
        #region 全角转换半角以及半角转换为全角
        ///字符串转换为全角(full-width)
        ///全角空格为12288，半角空格为32
        ///其他字符半角(33-126)与全角(65281-65374)的对应关系是：均相差65248
        public static string ToFullWidth(string input)
        {
            if (input == null)
            {
                return "";
            }
            // 半角转全角：
            var array = input.ToCharArray();
            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] == 32)
                {
                    array[i] = (char)12288;
                    continue;
                }
                if (array[i] < 127)
                {
                    array[i] = (char)(array[i] + 65248);
                }
            }
            return new string(array);
        }
        /// <summary>
        ///  Judge whether the string contains Chinese characters
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool ContainsChinese(string input) => Regex.IsMatch(input, @"[\u4e00-\u9fa5]");

        /// <summary>
        /// 字符串转换为半角(half-width)
        /// 全角空格为12288，半角空格为32;
        /// 其他字符半角(33-126)与全角(65281-65374)的对应关系是：均相差65248
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ToHalfWidth(string input)
        {
            if (input == null)
            {
                return "";
            }
            var array = input.ToCharArray();
            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] == 12288)
                {
                    array[i] = (char)32;
                    continue;
                }
                if (array[i] > 65280 && array[i] < 65375)
                {
                    array[i] = (char)(array[i] - 65248);
                }
            }
            return new string(array);
        }
        #endregion

        /// <summary>
        /// Convert string to unicode representation string. e.g. 你好 -> \u4F60\u597D
        /// </summary>
        /// <param name="value"></param>
        /// <param name="encoding"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public static string StringEncode(string value, Encoding? encoding = null, string prefix = "\\u")
        {
            encoding ??= Encoding.Unicode;
            var enumerator = StringInfo.GetTextElementEnumerator(value);//use string info instead of string.length to get exact iterator of unicode characters. because one unicode character may consist of more than two chars.
            var sb = new StringBuilder();
            while (enumerator.MoveNext())
            {
                sb.Append(GetCodePoint(enumerator.GetTextElement(), prefix, encoding));
            }

            return sb.ToString();

            static string GetCodePoint(string character, string prefix, Encoding encoding)
            {
                var retVal = prefix;
                var bytes = encoding.GetBytes(character);
                for (var ctr = bytes.Length - 1; ctr >= 0; ctr--)
                    retVal += bytes[ctr].ToString("X2");
   
                return retVal;
            }
        }
        /// <summary>
        /// Convert unicode representation string to string. e.g. \u4F60\u597D -> 你好
        /// </summary>
        /// <param name="value"></param>
        /// <param name="encoding"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public static string StringDecode(string value, Encoding? encoding = null, string prefix = "\\u")
        {
            encoding ??= Encoding.Unicode;
            return Regex.Replace(
                value,
                $@"{prefix.ToRegexEscaped()}([a-zA-Z0-9]+)",
                m =>
                {
                    var hexStr = m.Groups[1].Value;
                    hexStr = hexStr.Length.IsEven() ? hexStr : "0" + hexStr;
                    var list = new List<byte>();
                    for (var i = hexStr.Length - 1; i >= 0; i-=2)
                    {
                        var hex = hexStr.Substring(i - 1, 2);
                        list.Add((byte) int.Parse(hex, NumberStyles.HexNumber));
                    }
                    return encoding.GetString(list.ToArray());
                });
        }

        /// <summary>
        /// [LevenshteinDistance] Get similarity ratio of given two strings.
        /// </summary>
        /// <param name="str1"></param>
        /// <param name="str2"></param>
        /// <returns></returns>
        public static double Similarity(string str1, string str2) => LevenshteinDistance.Similarity(str1, str2);
        /// <summary>
        /// [LevenshteinDistance] Calculate the difference between 2 strings using the Levenshtein distance algorithm
        /// </summary>
        /// <param name="str1">First string</param>
        /// <param name="str2">Second string</param>
        /// <returns></returns>
        public static int Calculate(string str1, string str2) => LevenshteinDistance.Calculate(str1, str2);

        #region Unicode与Ascii转换
        public static string UnicodeEncode(string value, bool notEncodeAscii = false)
        {
            var sb = new StringBuilder();
            foreach (var c in value)
            {
                if (notEncodeAscii && c <= 127)
                {
                    sb.Append(c);
                }
                else
                {
                    var encodedValue = "\\u" + ((int)c).ToString("x4").ToUpperInvariant();
                    sb.Append(encodedValue);
                }
            }
            return sb.ToString();
        }

        public static string UnicodeDecode(string value)
        {
            return Regex.Replace(
                value,
                @"\\u([a-zA-Z0-9]{4})",
                m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber)).ToString());
        }

        #endregion
    }
}

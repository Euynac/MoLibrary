using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// 转义工具类 用于转义一些在某些场合可能引发问题的字符串
    /// </summary>
    public static class EscapeTool
    {
        /// <summary>
        /// The same as Regex.Escape.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static string ToRegexEscaped(this string pattern) => Regex.Escape(pattern);
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
}

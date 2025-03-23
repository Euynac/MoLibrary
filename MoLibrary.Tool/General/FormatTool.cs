using System;
using System.Text;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// General format style for some scenario
    /// </summary>
    public static class FormatTool
    {
        public static char ToUpperLetter(this int num)
        {
            if (num is < 1 or > 26) throw new Exception("number to letter only support 1 - 26");
            return (char) ('A' + (num - 1));
        }

        public static int FromLetter(this char letter)
        {
            letter = letter.ToString().ToUpperInvariant()[0];
            if(letter is < 'A' or > 'Z') throw new Exception("letter to number only support a - z or A-Z");
            return letter - 'A' + 1;
        }

        private static readonly string[] _sizeUnit = {
            "B",
            "KB",
            "MB",
            "GB",
            "TB",
            "PB"
        };

        /// <summary>
        /// Returns a formatted string representing the size, up to PB, especially of the file length defined in the FileInfo instance (i.e. 10 KB).
        /// </summary>
        /// <param name="bytes">The bytes value.</param>
        /// <from>CSharpLib</from>
        /// <returns>The equivalent formatted string of given bytes</returns>
        public static string FormatBytes(this long bytes)
        {
            double num = bytes;
            int i;
            for (i = 0; i < _sizeUnit.Length && bytes >= 1024L; bytes /= 1024L)
            {
                num = bytes / 1024.0;
                ++i;
            }
            return $"{num:0.###}{_sizeUnit[i]}";
        }
        /// <summary>
        /// Progress bar style format like "[|||||||||  ]"
        /// </summary>
        /// <param name="len">the whole bar's str length. (At least 3)</param>
        /// <param name="percentage">the progress percentage of whole bar</param>
        /// <returns></returns>
        public static string FormatProgressBar(int len, double percentage)
        {
            if (len <= 2) return null;
            var progressCount = (int)((len - 2) * percentage.LimitInRange(0, 1));
            var blankCount = len - 2 - progressCount;
            var result = new StringBuilder();
            result.Append("[");
            for (var i = 0; i < progressCount; i++)
            {
                result.Append("|");
            }
            for (var i = 0; i < blankCount; i++)
            {
                result.Append(" ");
            }

            result.Append("]");
            return result.ToString();
        }
    }
}
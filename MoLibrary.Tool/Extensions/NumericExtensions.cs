using System;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of Numeric type
    /// </summary>
    public static class NumericExtensions
    {
        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static int LimitInRange(this int num, int max) => num > max ? max : num;

        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="min">Minimum value, less than then take this minimum value</param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static int LimitInRange(this int num, int min, int max) => (num > max) ? max : (num < min) ? min : num;

        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static long LimitInRange(this long num, long max) => num > max ? max : num;

        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="min">Minimum value, less than then take this minimum value</param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static long LimitInRange(this long num, long min, long max) => num > max ? max : num < min ? min : num;

        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="min">Minimum value, less than then take this minimum value</param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static long LimitInRange(this long num, long? min, long? max)
        {
            if (min == null)
            {
                return max == null ? num : num.LimitInRange(max.Value);
            }

            return max == null ? num < min ? min.Value : num : num.LimitInRange(min.Value, max.Value);
        }
        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="min">Minimum value, less than then take this minimum value</param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static double LimitInRange(this double num, double min, double max) => (num > max) ? max : (num < min) ? min : num;

        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="min">Minimum value, less than then take this minimum value</param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static double LimitInRange(this double num, double? min, double? max)
        {
            if (min == null)
            {
                return max == null ? num : num.LimitInRange(max.Value);
            }

            return max == null ? num < min ? min.Value : num : num.LimitInRange(min.Value, max.Value);
        }
        
        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="min">Minimum value, less than then take this minimum value</param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static int LimitInRange(this int num, double? min, double? max)
        {
            if (min == null)
            {
                return max == null ? num : num > max ? (int)max.Value : num;
            }

            return max == null ? num < min ? (int)min.Value : num : num.LimitInRange((int)min.Value, (int)max.Value);
        }
        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="min">Minimum value, less than then take this minimum value</param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static long LimitInRange(this long num, double? min, double? max)
        {
            if (min == null)
            {
                return max == null ? num : num > max ? (long)max.Value : num;
            }

            return max == null ? num < min ? (long)min.Value : num : num.LimitInRange((long)min.Value, (long)max.Value);
        }
        /// <summary>
        /// Limit the specified number to a specific range
        /// </summary>
        /// <param name="num"></param>
        /// <param name="max">Maximum value, beyond which this maximum value is taken</param>
        /// <returns></returns>
        public static double LimitInRange(this double num, double max) => num > max ? max : num;
        /// <summary>
        /// Judge whether a double number is actually a integer.
        /// </summary>
        /// <param name="num"></param>
        /// <returns>Epsilon maybe up to 1-16e.</returns>
        public static bool IsInteger(this double num) => num % 1 == 0;
        /// <summary>
        /// 返回大于或等于指定double的最小整数值。
        /// Returns the smallest integer value greater than or equal to the specified double.
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static int Ceiling(this double num) => (int)Math.Ceiling(num);
        /// <summary>
        /// Returns the smallest integer value greater than or equal to the specified float.
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static int Ceiling(this float num) => (int)Math.Ceiling(num);
        /// <summary>
        /// Not same as P in string format which always retains 2 decimal places. It only retain decimal max to given <paramref name="maxRetain"/>. 
        /// </summary>
        /// <param name="num"></param>
        /// <param name="maxRetain"></param>
        /// <returns></returns>
        public static string FormatPercentage(this double num, int maxRetain = 2) => (num * 100).ToString("0." + '#'.Repeat(maxRetain)) + '%';

        /// <summary>
        /// Not same as P in string format which always retains 2 decimal places. It only retain decimal max to given <paramref name="maxRetain"/>. 
        /// </summary>
        /// <param name="num"></param>
        /// <param name="maxRetain"></param>
        /// <returns></returns>
        public static string FormatPercentage(this float num, int maxRetain = 2) =>
            FormatPercentage((double) num, maxRetain);

        public static int Truncate(this double num) => (int)Math.Truncate(num);
        public static double Round(this double num, int leftDecimalCount = 2, MidpointRounding rounding = MidpointRounding.AwayFromZero) =>
            Math.Round(num, leftDecimalCount, rounding);
        /// <summary>
        ///  Check whether the two numbers are nearly equal in given tolerance.
        /// </summary>
        /// <param name="num1"></param>
        /// <param name="num2"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static bool NearlyEqual(this double num1, double num2, double tolerance = 1e-8)
        {
            return Math.Abs(num1 - num2) < tolerance;
            //var abs1 = Math.Abs(num1);
            //var abs2 = Math.Abs(num2);
            //var diff = Math.Abs(num1 - num2);

            //if (num1 == num2)
            //{
            //    return true;
            //}
            //else if (num1 == 0 || num2 == 0 || diff < double.Epsilon)
            //{
            //    return diff < epsilon;
            //}
            //else
            //{
            //    return diff / (abs1 + abs2) < epsilon;
            //}

        }
    }
}
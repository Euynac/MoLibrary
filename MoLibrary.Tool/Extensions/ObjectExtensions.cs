using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of object or generic type.
    /// </summary>
    public static partial class ObjectExtensions
    {
        #region Format
        /// <summary>
        /// Returns the given string directly. Use must obj?.Be() in this way to quickly truncate to null.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <returns></returns>
        public static string? Be([NotNull] this object obj, string? be) => be;
        /// <summary>
        /// Returns the given string directly. Use must obj?.Be() in this way to quickly truncate to null.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <param name="useStringFormat">Placeholder, either true or false will still use string.Format, which the {0} will represent the passed obj.</param>
        /// <returns></returns>
        [StringFormatMethod("be")]
        public static string? Be([NotNull] this object obj, string? be, bool useStringFormat) => be == null ? null : string.Format(be, obj);
        /// <summary>
        /// Returns given front append with object. Use must obj?.Be() in this way to quickly truncate to null.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="fontAppend"></param>
        public static string BeAfter(this object obj, string fontAppend) => $"{fontAppend}{obj}";
        /// <summary>
        /// If the string is not null or Empty, the given string is returned directly.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <returns></returns>
        public static string? BeIfNotEmpty(this string? obj, string? be) => string.IsNullOrEmpty(obj) ? null : be;

        /// <summary>
        /// If the string is not null or Empty, the given string is returned directly.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <param name="useStringFormat">Placeholder, either true or false will still use string.Format, which the {0} will represent the passed obj.</param>
        /// <returns></returns>
        [StringFormatMethod("be")]
        public static string? BeIfNotEmpty(this string? obj, string? be, bool useStringFormat)
        {
            return string.IsNullOrEmpty(obj) ? null : string.Format(be ?? string.Empty, obj);
        }
        /// <summary>
        /// If the string is not null, "" or WhiteSpace, the given string is returned directly.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <returns></returns>
        public static string? BeIfNotWhiteSpace(this string? obj, string? be) => string.IsNullOrWhiteSpace(obj) ? null : be;
        /// <summary>
        /// If the string is null, "" or WhiteSpace, return null directly
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string? BeNullIfWhiteSpace(this string? obj) => string.IsNullOrWhiteSpace(obj) ? null : obj;
        /// <summary>
        /// If the string is null or "", return null directly
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string? BeNullIfEmpty(this string? obj) => string.IsNullOrEmpty(obj) ? null : obj;
        /// <summary>
        /// Returns the given customFormat if the list contains the specified element, otherwise returns null.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="item"></param>
        /// <param name="customFormat"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static string? BeIfContains<T>(this IEnumerable<T> list, T item,
            string customFormat)
            => list.Contains(item) ? customFormat : null;

        /// <summary>
        /// Returns null if false, otherwise returns given string.
        /// </summary>
        /// <param name="isTrue"></param>
        /// <param name="be"></param>
        /// <returns></returns>
        public static string? BeIfTrue(this bool isTrue, string? be) => isTrue ? be : null;
        /// <summary>
        /// If the value type is the initial default value then return the null string, otherwise return given string (used when splicing strings) (note that special cases such as int=0 if also valid)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <returns></returns>
        public static string? BeIfNotDefault<T>(this T obj, string be) where T : struct //值类型约束
            => obj.Equals(default(T)) ? null : be;
     
        /// <summary>
        /// Returns null if the value type is the initial default value, otherwise returns itself
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? BeNullIfDefault<T>(this T obj) where T : struct
            => obj.Equals(default(T)) ? null : obj;
        /// <summary>
        /// If the value type is the initial default value then return the null string, otherwise return given string (used when splicing strings) (note that special cases such as int=0 if also valid)
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <param name="useStringFormat">Placeholder, either true or false will still use string.Format, which the {0} will represent the passed obj.</param>
        /// <returns></returns>
        [StringFormatMethod("be")]
        public static string? BeIfNotDefault<T>(this T obj, string? be, bool useStringFormat) where T : struct //值类型约束
        {
            return obj.Equals(default(T)) ? null : string.Format(be ?? string.Empty, obj);
        }
        /// <summary>
        /// Returns the null string if the set is null or Count = 0, otherwise returns given string. (used when splicing strings)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="be"></param>
        /// <returns></returns>
        public static string? BeIfNotEmptySet<T>(this IEnumerable<T> obj, string be) 
            => obj.IsNullOrEmptySet() ? null : be;

        #endregion
        /// <summary>
        /// Returns one of two parts, depending on the given flag.
        /// </summary>
        /// <param name="flag">return depending on the flag.</param>
        /// <param name="trueReturn">return this if flag is true.</param>
        /// <param name="falseReturn">return this if flag is false.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static object? IIf(this bool flag, object? trueReturn, object? falseReturn) =>
            flag ? trueReturn : falseReturn;
        /// <summary>
        /// Returns one of two parts, depending on the given flag.
        /// </summary>
        /// <param name="flag">return depending on the flag.</param>
        /// <param name="trueReturn">return this if flag is true.</param>
        /// <param name="falseReturn">return this if flag is false or null.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static object? IIf(this bool? flag, object? trueReturn, object? falseReturn) =>
            flag == null ? falseReturn : flag.Value ? trueReturn : falseReturn;
        /// <summary>
        /// Returns one of two parts, depending on the given flag.
        /// </summary>
        /// <param name="flag">return depending on the flag.</param>
        /// <param name="trueReturn">return this if flag is true.</param>
        /// <param name="falseReturn">return this if flag is false.</param>
        /// <param name="nullReturn">return this if flag is null.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static object? IIf(this bool? flag, object? trueReturn, object? falseReturn, object? nullReturn) =>
            flag == null ? nullReturn : flag.Value ? trueReturn : falseReturn;
        /// <summary>
        /// Returns one of two parts, depending on the given flag.
        /// </summary>
        /// <param name="flag">return depending on the flag.</param>
        /// <param name="trueReturn">return this if flag is true.</param>
        /// <param name="falseReturn">return this if flag is false.</param>
        /// <param name="nullReturn">return this if flag is null.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static T? IIf<T>(this bool? flag, T? trueReturn, T? falseReturn, T? nullReturn) =>
            flag == null ? nullReturn : flag.Value ? trueReturn : falseReturn;
        /// <summary>
        /// Returns one of two parts, depending on the given flag.
        /// </summary>
        /// <param name="flag">return depending on the flag.</param>
        /// <param name="trueReturn">return this if flag is true.</param>
        /// <param name="falseReturn">return this if flag is false or null.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static T? IIf<T>(this bool? flag, T? trueReturn, T? falseReturn) =>
            flag == null ? falseReturn : flag.Value ? trueReturn : falseReturn;
        /// <summary>
        /// Returns one of two parts, depending on the given flag.
        /// </summary>
        /// <param name="flag">return depending on the flag.</param>
        /// <param name="trueReturn">return this if flag is true.</param>
        /// <param name="falseReturn">return this if flag is false.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static T? IIf<T>(this bool flag, T? trueReturn, T? falseReturn) =>
            flag ? trueReturn : falseReturn;

        /// <summary>
        /// 判断是否存在一个元素给定的元素与之相等。
        /// <br/>English: Determine whether there is an element given element is equal to it.
        /// </summary>
        /// <typeparam name="T">可以为空</typeparam>
        /// <param name="this"></param>
        /// <param name="objects">给定的元素</param>
        /// <returns></returns>
        public static bool EqualsAny<T>(this T? @this, params T?[] objects) where T : class
        {
            if (@this == null && objects.Any(obj => obj == null)) return true;
            return @this != null && objects.Any(@this.Equals);
        }

        /// <summary>
        /// 判断是否存在一个元素给定的元素与之相等。
        /// <br/>English: Determine whether there is an element given element is equal to it.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="this"></param>
        /// <param name="objects">给定的元素</param>
        /// <returns></returns>
        public static bool EqualsAny<T>(this T? @this, params T?[] objects) where T : struct
        {
            if (@this == null && objects.Any(obj => obj == null)) return true;
            return objects.Any(obj => obj.Equals(@this));
        }

        /// <summary>
        /// 判断是否存在一个元素给定的元素与之相等。
        /// <br/>English: Determine whether there is an element given element is equal to it.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="this"></param>
        /// <param name="objects">给定的元素</param>
        /// <returns></returns>
        public static bool EqualsAny<T>(this T @this, params T?[] objects) where T : struct
        {
            return objects.Any(obj => obj.Equals(@this));
        }
        /// <summary>
        /// 判断元素是否满足任意一个方法。
        /// <br/>English: Determine whether the element satisfies any method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool SatisfyAny<T>(this T obj, params Func<T, bool>[] predicates) where T : class
        {
            return predicates.Any(p => p.Invoke(obj));
        }

        /// <summary>
        /// 判断元素是否满足所有方法。
        /// <br/>English: Determine whether the element satisfies all methods.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool SatisfyAll<T>(this T obj, params Func<T, bool>[] predicates) where T : class
        {
            return predicates.All(p => p.Invoke(obj));
        }

        public static bool Each<T>(this T tuples, Func<dynamic, bool> predicate) where T : ITuple
        {
            var result = true;
            for (var i = 0; i < tuples.Length; i++)
            {
                result &= predicate.Invoke(tuples[i]);
            }
            return result;
        }
    }
}
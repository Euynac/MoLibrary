using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of IEnumerable type
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Concatenates the members of a collection, using the specified separator between each member.
        /// </summary>
        /// <param name="separator">The string to use as a separator.separator is included in the returned string only if values has more than one element.</param>
        /// <param name="values">A collection that contains the objects to concatenate.</param>
        /// <typeparam name="T">The type of the members of values.</typeparam>
        /// <returns>A string that consists of the members of <paramref name="values">values</paramref> delimited by the <paramref name="separator">separator</paramref> string. If <paramref name="values">values</paramref> has no members, the method returns <see cref="F:System.String.Empty"></see>.</returns>
        public static string StringJoin<T>(this IEnumerable<T>? values, string separator) =>
            values == null ? string.Empty : string.Join(separator, values);
        /// <summary>
        /// Concatenates the members of a collection, using the specified separator between each member.
        /// </summary>
        /// <param name="separator">The char to use as a separator.separator is included in the returned string only if values has more than one element.</param>
        /// <param name="values">A collection that contains the objects to concatenate.</param>
        /// <typeparam name="T">The type of the members of values.</typeparam>
        /// <returns>A string that consists of the members of <paramref name="values">values</paramref> delimited by the <paramref name="separator">separator</paramref> string. If <paramref name="values">values</paramref> has no members, the method returns <see cref="F:System.String.Empty"></see>.</returns>
        public static string StringJoin<T>(this IEnumerable<T>? values, char separator) =>
            values == null ? string.Empty : string.Join(separator.ToString(), values);

        /// <summary>
        /// 判断一个集合是否是 null 或空集合。
        /// <br/>English: Determine whether a collection is null or an empty collection.
        /// </summary>
        /// <param name="collection">指定的集合</param>
        /// <returns></returns>
        [ContractAnnotation("null => true")]
        public static bool IsNullOrEmptySet<T>([NotNullWhen(false)] [NoEnumeration] this IEnumerable<T>? collection) //指示不会对collection进行读写操作，但这里读了?
            => collection == null || !collection.Any();

        /// <summary>
        /// 判断一个集合是否是 null 或空集合。
        /// <br/>English: Determine whether a collection is null or an empty collection.
        /// </summary>
        /// https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/nullable-analysis
        [ContractAnnotation("null => true")] //能够教会ReSharper空判断(传入的是null，返回true)
        public static bool IsNullOrEmptySet([NotNullWhen(false)] [NoEnumeration] this IEnumerable? @this) => @this == null || !@this.GetEnumerator().MoveNext();

        /// <summary>
        /// If given value not null, it will add into list, otherwise, ignore the value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="valueToAdd"></param>
        public static void AddIfNotNull<T>(this List<T> list, T? valueToAdd)
        {
            if(valueToAdd == null) return;
            list.Add(valueToAdd);
        }
        /// <summary>
        /// Add into list for specific count of custom values using given function to get. (usually use in initialize list with default value). 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="customValueFunc">Add this method return value to add into list.</param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static List<T> AddRepeatValue<T>(this List<T> list, int count, Func<T> customValueFunc)
        {
            for (var i = 0; i < count; i++)
            {
                list.Add(customValueFunc.Invoke());
            }

            return list;
        }
        /// <summary>
        /// Add into list for specific count of custom values. (usually use in initialize list with default value).
        /// <para>Must take attention when use reference type to add into list, because it reference to same object.</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="customValue">e.g. default(T) Must take attention when use reference type, because it reference to same object.</param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static List<T> AddRepeatValue<T>(this List<T> list, T customValue, int count)
        {
            for (var i = 0; i < count; i++)
            {
                list.Add(customValue);
            }

            return list;
        }
        /// <summary>
        /// 尝试获取与指定的键相关联的值。
        /// <br/>English: Attempts to get the value associated with the specified key.
        /// </summary>
        /// <param name="dict">可为空</param>
        /// <param name="value">当本方法返回时，如果找到了指定的键，则返回与该键相关联的值；否则，返回值参数类型的默认值或设定的值。这个参数是在未初始化的情况下传递的。
        ///<br/>English: When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter or the specified value. This parameter is passed uninitialized.</param>
        /// <param name="key">要获取值的键，可为空，为空必返回false。
        ///  <br/>English: The key of the value to get. key can be null, if null, return false.
        /// </param>
        /// <param name="defaultValue">失败时返回的默认值或设定的值。
        /// <br/>English: The default value or the specified value to return if failed.
        /// </param>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <returns></returns>
        [ContractAnnotation("dict:null => false; key:null => false")]
        public static bool TryGetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue>? dict, TKey? key, out TValue? value, TValue? defaultValue = default)
        {
            if (dict.IsNullOrEmptySet() || key == null)
            {
                value = defaultValue;
                return false;
            }
            if (dict.TryGetValue(key, out value)) return true;
            value = defaultValue;
            return false;
        }

        /// <summary>
        /// 尝试通过Value获取Key的值（多个value相同仅获取一个key，所以一般用于value和key一对一）
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="value">预测Dictionary中会有的值</param>
        /// <param name="key">若是存在value将返回key</param>
        /// <returns>成功返回true且返回key，不成功则返回false</returns>
        public static bool TryGetKey<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>>? dict, TValue value, out TKey? key)
        {
            key = default;
            if (dict.IsNullOrEmptySet()) return false;
            foreach (var pair in dict)
            {
                if (pair.Value != null && pair.Value.Equals(value))
                {
                    key = pair.Key;
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 尝试获取所有指定Value对应的Key值
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="value">预测集合中会有的值</param>
        /// <param name="key">value对应的所有Key</param>
        /// <returns>成功返回true且返回key的List，不成功则返回false</returns>
        public static bool TryGetAllKey<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>>? dict, TValue value, out List<TKey> key)
        {
            key = new List<TKey>();
            if (dict.IsNullOrEmptySet()) return false;
            foreach (var keyValuePair in dict)
            {
                if (keyValuePair.Value.Equals(value))
                {
                    key.Add(keyValuePair.Key);
                }
            }
            return key.Count != 0;
        }
        /// <summary>
        /// 将可空类型的集合转换为不可空的<seealso cref="IEnumerable{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static IEnumerable<T> ConvertToNotNullable<T>(this IEnumerable<T?> list) where T : struct
        {
            return list.Where(i => i != null).Select(i => i!.Value).ToList();
        }

        /// <summary>
        /// Cast current IEnumerable item to specific type IList. (Usually used in converting List&lt;object&gt; to List&lt;T&gt;)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        [Obsolete("Use official Cast<T> instead.")]
        public static IList<T> CastToList<T>(this IEnumerable source)
        {
            var listType = typeof(List<>).MakeGenericType(typeof(T));
            var list = (IList<T>)Activator.CreateInstance(listType);
            foreach (var item in source) list.Add((T)item);
            return list;
        }

        /// <summary>
        /// Usually use in foreach, when enumerable is null, this method will return count = 0 list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static IEnumerable<T> BeNotNull<T>(this IEnumerable<T>? enumerable) => enumerable ?? new List<T>();

        /// <summary>
        /// Foreach with index.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self">Support to iterate null.</param>
        /// <returns></returns>
        public static IEnumerable<(int index, T item)> WithIndex<T>(this IEnumerable<T>? self) => self?.Select((item, index) => (index, item)) ?? Enumerable.Empty<(int,T)>();

        /// <summary>
        /// Opposition of IEnumerable.Where.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static IEnumerable<T> Remove<T>(this IEnumerable<T> list, Func<T, bool> func) => list.Where(p => !func(p));
        /// <summary>
        /// Take specific item at given index to create a tuple.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="index1"></param>
        /// <param name="index2"></param>
        /// <returns></returns>
        public static (T? item1, T? item2) TakeTuple<T>(this IEnumerable<T> list, int index1, int index2)
        {
            var tmp = list.ToList();
            return (tmp.ElementAtOrDefault(index1), tmp.ElementAtOrDefault(index2));
        }
        /// <summary>Do action for each item in given collection</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="action"></param>
        public static void Do<T>(this IEnumerable<T> collection, Action<T> action)
        {
            foreach (var obj in collection)
            {
                if (obj != null)
                    action(obj);
            }
        }

        /// <summary>
        /// Do action asynchronously for each item in given collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="action"></param>
        public static async Task DoAsync<T>(this IEnumerable<T> collection, Func<T, Task> action)
        {
            foreach (var obj in collection)
            {
                if (obj != null)
                    await action(obj);
            }
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of IDictionary type.
    /// </summary>
    public static class IDictionaryExtensions
    {
        /// <summary>
        /// Try to add keys and values to the dictionary, if they don't exist then add them, if they do exist then don't add them and don't throw exceptions
        /// </summary>
        public static IDictionary<TKey, TValue> AddOrIgnore<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (!dict.ContainsKey(key)) dict.Add(key, value);
            return dict;
        }

        /// <summary>
        /// Add keys and values to the dictionary, existing ones will be replaced. Actually a more straightforward way of writing dict[key] = value
        /// </summary>
        public static IDictionary<TKey, TValue> AddOrReplace<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            dict[key] = value;
            return dict;
        }

        /// <summary>
        /// Adding key-value pairs to the dictionary in bulk
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="values"></param>
        /// <param name="replaceExisted">If it already exists, then replaced</param>
        public static IDictionary<TKey, TValue> AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dict, IEnumerable<KeyValuePair<TKey, TValue>> values, bool replaceExisted = false)
        {
            foreach (var pair in values)
            {
                if (dict.ContainsKey(pair.Key) == false || replaceExisted)
                    dict[pair.Key] = pair.Value;
            }
            return dict;
        }
   
        /// <summary>
        /// Create case insensitive dictionary from <see cref="IEnumerable{T}">IEnumerable</see>.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static Dictionary<string, TValue> ToIgnoreCaseDictionary<TValue>(
            this IEnumerable<KeyValuePair<string, TValue>> enumerable) => new(enumerable, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Create case insensitive dictionary from given dictionary.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="oldDictionary"></param>
        /// <returns></returns>
        public static Dictionary<string, TValue> ToIgnoreCaseDictionary<TValue>(this Dictionary<string, TValue> oldDictionary) => new(oldDictionary, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 获取与指定的键相关联的值，如果没有则返回指定的默认值或Value类的默认值（引用类型默认值null，值类型返回0或false或'\0'等）（好处：不用写ContainsKey）
        /// <br/>English: Gets the value associated with the specified key, or returns the specified default value or the default value of the Value class if not (reference type default value null, value type returns 0 or false or '\0', etc.) (advantage: no need to write ContainsKey)
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="key">如果是null也会返回默认值</param>
        /// <param name="defaultValue">指定默认值</param>
        /// <returns></returns>
        public static TValue? GetValueOrCustom<TKey, TValue>(this IDictionary<TKey, TValue>? dict, TKey? key, TValue? defaultValue = default)
        {
            if (key == null || dict == null) return defaultValue;
            return dict.TryGetValue(key, out var result) == false ? defaultValue : result;
        }

        /// <summary>
        /// 检查指定字典中是否存在任意一个给定的元素
        /// <br/>English: Check if any of the given elements exist in the specified dictionary
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        [ContractAnnotation("dict:null => false; keys:null => false")]
        public static bool ContainsAny<TKey, TValue>(this IDictionary<TKey, TValue>? dict, params TKey[]? keys)
        {
            if (keys == null || dict.IsNullOrEmptySet()) return false;
            return keys.Any(dict.ContainsKey);
        }
        /// <summary>
        /// 检查指定字典中是否存在任意一个给定的元素
        /// <br/>English: Check if any of the given elements exist in the specified dictionary
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        [ContractAnnotation("dict:null => false; keys:null => false")]
        public static bool ContainsAll<TKey, TValue>(this IDictionary<TKey, TValue>? dict, params TKey[]? keys)
        {
            if (keys == null || dict.IsNullOrEmptySet()) return false;
            return keys.All(dict.ContainsKey);
        }

        /// <summary>
        /// Set element at specific index to given value.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="index"></param>
        /// <param name="setValue"></param>
        public static void SetElementAt<TKey, TValue>(this IDictionary<TKey, TValue>? dict, int index, TValue setValue)
        {
            if(dict == null || dict.IsNullOrEmptySet()) return;
            dict[dict.ElementAt(index).Key] = setValue;
        }

        /// <summary>
        /// Get IEnumerable&lt;KeyValuePair&lt;object, object?&gt;&gt; from IDictionary.
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public static IEnumerable<KeyValuePair<object, object?>> GetEnumerable(this IDictionary dict)
        {
            return from DictionaryEntry entry in dict select new KeyValuePair<object, object?>(entry.Key, entry.Value);
        }
    }
}
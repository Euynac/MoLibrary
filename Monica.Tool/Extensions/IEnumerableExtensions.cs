using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;

namespace Monica.Tool.Extensions;

public static class IEnumerableExtensions
{
    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    /// <typeparam name="T">Type of the items in the collection</typeparam>
    /// <param name="source">The collection</param>
    /// <param name="items">Items to be removed from the list</param>
    public static void RemoveAll<T>(this ICollection<T> source, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            source.Remove(item);
        }
    }



    #region List
    public static void AddFirst<T>(this IList<T> source, T item)
    {
        source.Insert(0, item);
    }

    public static void AddLast<T>(this IList<T> source, T item)
    {
        source.Insert(source.Count, item);
    }
    public static int FindIndex<T>(this IList<T> source, Predicate<T> selector)
    {
        for (var i = 0; i < source.Count; ++i)
        {
            if (selector(source[i]))
            {
                return i;
            }
        }

        return -1;
    }
    #endregion


    /// <summary>Do action for each item in given collection</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="action"></param>
    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (var obj in collection)
        {
            if (obj != null)
                action(obj);
        }
    }

   
    /// <summary>
     /// Adds an item to the collection if it's not already in the collection.
     /// </summary>
     /// <param name="source">The collection</param>
     /// <param name="item">Item to check and add</param>
     /// <typeparam name="T">Type of the items in the collection</typeparam>
     /// <returns>Returns True if added, returns False if not.</returns>
    public static bool AddIfNotContains<T>(this ICollection<T> source, T item)
    {
        if (source.Contains(item))
        {
            return false;
        }

        source.Add(item);
        return true;
    }

    /// <summary>
    /// Adds items to the collection which are not already in the collection.
    /// </summary>
    /// <param name="source">The collection</param>
    /// <param name="items">Item to check and add</param>
    /// <typeparam name="T">Type of the items in the collection</typeparam>
    /// <returns>Returns the added items.</returns>
    public static IEnumerable<T> AddIfNotContains<T>(this ICollection<T> source, IEnumerable<T> items)
    {
        var addedItems = new List<T>();

        foreach (var item in items)
        {
            if (source.Contains(item))
            {
                continue;
            }

            source.Add(item);
            addedItems.Add(item);
        }

        return addedItems;
    }

    /// <summary>
    /// Adds an item to the collection if it's not already in the collection based on the given <paramref name="predicate"/>.
    /// </summary>
    /// <param name="source">The collection</param>
    /// <param name="predicate">The condition to decide if the item is already in the collection</param>
    /// <param name="itemFactory">A factory that returns the item</param>
    /// <typeparam name="T">Type of the items in the collection</typeparam>
    /// <returns>Returns True if added, returns False if not.</returns>
    public static bool AddIfNotContains<T>(this ICollection<T> source, Func<T, bool> predicate, Func<T> itemFactory)
    {
        if (source.Any(predicate))
        {
            return false;
        }

        source.Add(itemFactory());
        return true;
    }

    /// <summary>
    /// Filters a <see cref="IEnumerable{T}"/> by given predicate if given condition is true.
    /// </summary>
    /// <param name="source">Enumerable to apply filtering</param>
    /// <param name="condition">A boolean value</param>
    /// <param name="predicate">Predicate to filter the enumerable</param>
    /// <returns>Filtered or not filtered enumerable based on <paramref name="condition"/></returns>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
    {
        return condition
            ? source.Where(predicate)
            : source;
    }

    /// <summary>
    /// Filters a <see cref="IEnumerable{T}"/> by given predicate if given condition is true.
    /// </summary>
    /// <param name="source">Enumerable to apply filtering</param>
    /// <param name="condition">A boolean value</param>
    /// <param name="predicate">Predicate to filter the enumerable</param>
    /// <returns>Filtered or not filtered enumerable based on <paramref name="condition"/></returns>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, int, bool> predicate)
    {
        return condition
            ? source.Where(predicate)
            : source;
    }
    /// <summary>
    /// Convert to a specific list type
    /// </summary>
    /// <param name="source"></param>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public static IList ConvertToSpecificItemType(this List<object> source, Type itemType)
    {
        var listType = typeof(List<>);
        var genericListType = listType.MakeGenericType([itemType]);
        var typedList = (IList) Activator.CreateInstance(genericListType)!;
        foreach (var item in source)
        {
            typedList.Add(item);
        }
        return typedList;
    }

    /// <summary>
    /// Merge lists into a new list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="listToCombine"></param>
    /// <remarks>Concat can be used instead</remarks>
    /// <returns></returns>
    public static List<T> CombineList<T>(this List<T> list, params List<T>?[]? listToCombine) =>
        list.CombineForeach(listToCombine?.Where(p => p != null).SelectMany(p => p!)).ToList();

    /// <summary>
    /// Batch iterate over list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="listToIterate"></param>
    /// <returns></returns>
    public static IEnumerable<T> CombineForeach<T>(this IEnumerable<T> list, params IEnumerable<T>?[]? listToIterate)
    {
        foreach (var item in list)
        {
            yield return item;
        }
        if (listToIterate != null)
        {
            foreach (var otherList in listToIterate)
            {
                if (otherList is null) continue;
                foreach (var item in otherList)
                {
                    yield return item;
                }
            }
        }
    }

    /// <summary>Modify item using given action in given collection</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="action"></param>
    public static IEnumerable<T> Doing<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (var obj in collection)
        {
            if (obj == null) continue;
            action(obj);
            yield return obj;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T DoWith<T>(this T obj, Action<T> action) where T:class
    {
        action(obj);
        return obj;
    }

    /// <summary>
    /// Take the intersection of the original set and the target state set, obtain the changes, and get three lists: to be deleted, to be updated, and to be added.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="originList"></param>
    /// <param name="finalList"></param>
    /// <returns></returns>
    public static (IList<T> deleting, IList<T> updating, IList<T> adding) IntersectGetChange<T>(this IList<T> originList, IList<T> finalList)
    {
        var deleting = new List<T>();
        var updating = new List<T>();
        var adding = new List<T>();
        var originHash = originList.ToHashSet();
        var finalHash = finalList.ToHashSet();

        foreach (var ori in originList)
        {
            if (finalHash.Contains(ori))
            {
                updating.Add(ori);
            }
            else
            {
                deleting.Add(ori);
            }
        }
        adding = finalList.Where(p => !originHash.Contains(p)).ToList();
        return (deleting, updating, adding);
    }
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
    /// Determine whether a collection is null or an empty collection.
    /// <br/>English: Determine whether a collection is null or an empty collection.
    /// </summary>
    /// <param name="collection">specified collection</param>
    /// <returns></returns>
    [ContractAnnotation("null => true")]
    public static bool IsNullOrEmptySet<T>([NotNullWhen(false)] this IEnumerable<T>? collection)
    {
        return collection switch
        {
            null => true,
            ICollection<T> genericCollection => genericCollection.Count == 0,
            IReadOnlyCollection<T> readOnlyCollection => readOnlyCollection.Count == 0,
            ICollection nonGenericCollection => nonGenericCollection.Count == 0,
            _ => !collection.Any()
        };
    }

    /// <summary>
    /// Determine whether a collection is null or an empty collection.
    /// <br/>English: Determine whether a collection is null or an empty collection.
    /// </summary>
    /// https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/nullable-analysis
    [ContractAnnotation("null => true")] //能够教会ReSharper空判断(传入的是null，返回true)
    public static bool IsNullOrEmptySet([NotNullWhen(false)] this IEnumerable? @this)
    {
        if (@this is null) return true;
        if (@this is ICollection collection) return collection.Count == 0;

        var enumerator = @this.GetEnumerator();
        try
        {
            return !enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// If given value not null, it will add into list, otherwise, ignore the value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="valueToAdd"></param>
    public static void AddIfNotNull<T>(this List<T> list, T? valueToAdd)
    {
        if (valueToAdd == null) return;
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
    /// Try to get the value associated with the specified key.
    /// <br/>English: Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="dict">Can be null</param>
    /// <param name="value">When this method returns, if the specified key is found, the value associated with the key is returned; otherwise, the default value or the set value of the value parameter type is returned. This parameter is passed uninitialized.
    ///<br/>English: When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter or the specified value. This parameter is passed uninitialized.</param>
    /// <param name="key">The key to obtain the value, which can be empty. If it is empty, false will be returned.
    ///  <br/>English: The key of the value to get. key can be null, if null, return false.
    /// </param>
    /// <param name="defaultValue">The default value or set value returned on failure.
    /// <br/>English: The default value or the specified value to return if failed.
    /// </param>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    [ContractAnnotation("dict:null => false; key:null => false")]
    public static bool TryGetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue>? dict, TKey? key, out TValue? value, TValue? defaultValue = default)
    {
        if (dict is null || key is null)
        {
            value = defaultValue;
            return false;
        }
        if (dict.TryGetValue(key, out value)) return true;
        value = defaultValue;
        return false;
    }

    /// <summary>
    /// Try to get the value of Key through Value (only one key is obtained when multiple values ​​are the same, so it is generally used for one-to-one value and key)
    /// </summary>
    /// <param name="dict"></param>
    /// <param name="value">Predict the values ​​that will be in Dictionary</param>
    /// <param name="key">If value exists, key will be returned</param>
    /// <returns>Returns true and key if successful, false if unsuccessful.</returns>
    public static bool TryGetKey<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>>? dict, TValue value, out TKey? key)
    {
        key = default;
        if (dict is null) return false;
        foreach (var pair in dict)
        {
            if (EqualityComparer<TValue>.Default.Equals(pair.Value, value))
            {
                key = pair.Key;
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// Try to get all Key values ​​corresponding to the specified Value
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="dict"></param>
    /// <param name="value">Predict the values ​​that will be in the set</param>
    /// <param name="key">All keys corresponding to value</param>
    /// <returns>Returns true if successful and returns a List of keys, false if unsuccessful.</returns>
    public static bool TryGetAllKey<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>>? dict, TValue value, out List<TKey> key)
    {
        key = new List<TKey>();
        if (dict is null) return false;
        foreach (var keyValuePair in dict)
        {
            if (EqualityComparer<TValue>.Default.Equals(keyValuePair.Value, value))
            {
                key.Add(keyValuePair.Key);
            }
        }
        return key.Count != 0;
    }
    /// <summary>
    /// Convert a collection of nullable types to non-nullable <seealso cref="IEnumerable{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    public static IEnumerable<T> ConvertToNotNullable<T>(this IEnumerable<T?> list) where T : struct
    {
        return list.Where(i => i != null).Select(i => i!.Value).ToList();
    }

    /// <summary>
    /// Usually use in foreach, when enumerable is null, this method will return count = 0 list.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <returns></returns>
    public static IEnumerable<T> BeNotNull<T>(this IEnumerable<T>? enumerable) => enumerable ?? Enumerable.Empty<T>();

    /// <summary>
    /// Foreach with index.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="self">Support to iterate null.</param>
    /// <returns></returns>
    public static IEnumerable<(int index, T item)> WithIndex<T>(this IEnumerable<T>? self) => self?.Select((item, index) => (index, item)) ?? [];

    /// <summary>
    /// Opposition of IEnumerable.Where.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    public static IEnumerable<T> WhereNot<T>(this IEnumerable<T> list, Func<T, bool> func) => list.Where(p => !func(p));
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
        ArgumentNullException.ThrowIfNull(list);

        T? item1 = default;
        T? item2 = default;
        var lastIndex = Math.Max(index1, index2);
        if (lastIndex < 0) return (item1, item2);

        var index = 0;
        foreach (var item in list)
        {
            if (index == index1) item1 = item;
            if (index == index2) item2 = item;
            if (index == lastIndex) break;
            index++;
        }

        return (item1, item2);
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

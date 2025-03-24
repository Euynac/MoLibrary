using JetBrains.Annotations;
using System.Collections;
using System.Reflection;


namespace BuildingBlocksPlatform.Extensions;

public class NoDisplayAttribute : Attribute
{
}

public static class IEnumerableExtensions
{
    /// <summary>
    /// Removes all item that match the condition from given list.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    public static void RemoveAll<T>(this IList<T> list, Func<T, bool> func)
    {
        for (int index = list.Count - 1; index >= 0; --index)
        {
            if (func(list[index]))
                list.RemoveAt(index);
        }
    }


    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    /// <typeparam name="T">Type of the items in the collection</typeparam>
    /// <param name="source">The collection</param>
    /// <param name="items">Items to be removed from the list</param>
    public static void RemoveAll<T>([NotNull] this ICollection<T> source, IEnumerable<T> items)
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
    /// 转换为特定的列表类型
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
    /// 利用反射机制将类型T内每个字段转成键值对
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    public static List<Dictionary<string, string>> FieldsToKeyValues<T>(this IList<T> list)
    {
        List<Dictionary<string, string>> result = new();
        foreach (var item in list)
        {
            Dictionary<string, string> pair = new();
            foreach (var propertyInfo in item.GetType().GetProperties())
            {
                if (propertyInfo != null && propertyInfo.GetCustomAttribute(typeof(NoDisplayAttribute)) == null)
                {
                    var value = propertyInfo.GetValue(item);
                    if (value == null)
                    {
                        pair.Add(propertyInfo.Name, "");
                    }
                    else if (value is DateOnly dateValue)
                    {
                        pair.Add(propertyInfo.Name, dateValue.ToString("yyyy-MM-dd"));
                    }
                    else
                    {
                        pair.Add(propertyInfo.Name, value.ToString() ?? "");
                    }
                }
            }
            result.Add(pair);
        }
        return result;
    }

    /// <summary>
    /// 合并列表为一个新的列表
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="listToCombine"></param>
    /// <remarks>可使用Concat替代</remarks>
    /// <returns></returns>
    public static List<T> CombineList<T>(this List<T> list, params List<T>?[]? listToCombine) =>
        list.CombineForeach(listToCombine?.Where(p => p != null).SelectMany(p => p!)).ToList();

    /// <summary>
    /// 批量迭代列表
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
    /// 对原始集合和目标状态集合取交集，获取变更，并得到三个列表：待删除，待更新，待添加
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
}

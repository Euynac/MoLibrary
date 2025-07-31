using System;
using System.Collections.Generic;
using System.Linq;

namespace MoLibrary.Tool.Extensions;

public static class ListExtensions
{
    /// <summary>
    /// 将List分为指定批大小的多批List
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="givenChunkSize"></param>
    /// <returns></returns>
    public static IEnumerable<List<T>> SplitIntoChunks<T>(this List<T> list, int? givenChunkSize)
    {
        if (givenChunkSize is not {} chunkSize)
        {
            yield return list;
            yield break;
        }

        if (chunkSize >= list.Count)
        {
            yield return list;
            yield break;
        }

        for (var i = 0; i < list.Count; i += chunkSize)
        {
            yield return list.GetRange(i, Math.Min(chunkSize, list.Count - i));
        }
    }

    /// <summary>
    /// 根据指定条件将列表分为满足条件和不满足条件的两个列表，只遍历一次
    /// </summary>
    /// <typeparam name="T">列表元素类型</typeparam>
    /// <param name="source">源列表</param>
    /// <param name="predicate">筛选条件</param>
    /// <returns>元组，第一个列表为满足条件的元素，第二个列表为不满足条件的元素</returns>
    public static (List<T> matched, List<T> unmatched) WherePartition<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        var matched = new List<T>();
        var unmatched = new List<T>();

        foreach (var item in source)
        {
            if (predicate(item))
                matched.Add(item);
            else
                unmatched.Add(item);
        }

        return (matched, unmatched);
    }
}
using System;
using System.Collections.Generic;

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
}
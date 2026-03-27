namespace Monica.Tool.Extensions;

public static class ListExtensions
{
    /// <summary>
    /// Divide List into multiple batches of List with specified batch size
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
    /// Divide the list into two lists that meet the conditions and those that do not meet the conditions according to the specified conditions, and only traverse once
    /// </summary>
    /// <typeparam name="T">list element type</typeparam>
    /// <param name="source">source list</param>
    /// <param name="predicate">Filter criteria</param>
    /// <returns>Tuple, the first list contains elements that meet the condition, and the second list contains elements that do not meet the condition</returns>
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
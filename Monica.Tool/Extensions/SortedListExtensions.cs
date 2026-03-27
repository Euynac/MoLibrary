namespace Monica.Tool.Extensions;

public class SortedListExtensions
{
    
}
/// <summary>
/// Comparator for comparing two keys, treating equal keys as smaller values ​​(first in, first out)
/// NOTE: This will break the Remove(key) or IndexOfKey(key) methods as the comparator will never return 0 for key equality
/// Applies to SortedList or SortedDictionary that do not allow duplicate keys
/// </summary>
/// <typeparam name="TKey">The type of key must implement the IComparable interface</typeparam>
public class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable<TKey>
{
    /// <summary>
    /// Compare the size relationship between two keys
    /// </summary>
    /// <param name="x">The first key to compare</param>
    /// <param name="y">The second key to compare</param>
    /// <returns>
    /// If x is less than y, return a negative number;
    /// If x is equal to y, return -1 (indicating that x is less than y, ensuring first-in, first-out);
    /// If x is greater than y, return a positive number
    /// </returns>
    public int Compare(TKey? x, TKey? y)
    {
        // Handling null value cases
        if (x is null && y is null) return -1; // 两个都为 null，返回 -1（表示 x 小于 y）保持 FIFO
        if (x is null) return -1; // x 为 null，返回 -1（表示 x 小于 y）
        if (y is null) return 1;  // y 为 null，返回 1（表示 x 大于 y）

        var result = x.CompareTo(y);
        return result == 0 ? -1 : result; // 相等时返回 -1（表示 x 小于 y）确保 FIFO 行为
    }
}
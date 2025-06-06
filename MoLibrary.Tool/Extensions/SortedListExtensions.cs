using System;
using System.Collections.Generic;

namespace MoLibrary.Tool.Extensions;

public class SortedListExtensions
{
    
}
/// <summary>
/// 用于比较两个键的比较器，将相等的键处理为较小值（先进先出）
/// 注意：这会破坏 Remove(key) 或 IndexOfKey(key) 方法，因为比较器永远不会返回 0 来表示键相等
/// 适用于不允许重复键的 SortedList 或 SortedDictionary
/// </summary>
/// <typeparam name="TKey">键的类型，必须实现 IComparable 接口</typeparam>
public class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable<TKey>
{
    /// <summary>
    /// 比较两个键的大小关系
    /// </summary>
    /// <param name="x">第一个要比较的键</param>
    /// <param name="y">第二个要比较的键</param>
    /// <returns>
    /// 如果 x 小于 y，返回负数；
    /// 如果 x 等于 y，返回 -1（表示 x 小于 y，确保先进先出）；
    /// 如果 x 大于 y，返回正数
    /// </returns>
    public int Compare(TKey? x, TKey? y)
    {
        // 处理 null 值情况
        if (x is null && y is null) return -1; // 两个都为 null，返回 -1（表示 x 小于 y）保持 FIFO
        if (x is null) return -1; // x 为 null，返回 -1（表示 x 小于 y）
        if (y is null) return 1;  // y 为 null，返回 1（表示 x 大于 y）

        var result = x.CompareTo(y);
        return result == 0 ? -1 : result; // 相等时返回 -1（表示 x 小于 y）确保 FIFO 行为
    }
}
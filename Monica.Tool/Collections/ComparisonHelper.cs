using JetBrains.Annotations;

namespace Monica.Tool.General;

/// <summary>
/// Provides comparison helpers for chained sorting and null-aware ordering.
/// </summary>
public static class ComparisonHelper
{
    /// <summary> 
    /// Put null value to the last of collections.By definition, any object compares greater than (or follows) null, two null references compare equal to each other, and true greater than false.
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="source"></param>
    /// <param name="keySelector">select null field not null</param>
    /// <returns></returns>
    [Obsolete("not recommend, only hint you the fact of definition")]
    public static IOrderedEnumerable<TSource> OrderNullToLast<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) => source.OrderByDescending(keySelector);

    /// <summary>
    /// Compare two objects in descending order, which is used for quick implementation of Compare related methods. It supports null comparison and chain comparison.
    /// (If they are not equal, return null (the comparison value will be returned only when the return value is null), truncate the method call, and the return value is the comparison result. Otherwise, you can chain execution until the two are not equal and then compare the next weight) (need to implement the IComparable interface)
    /// </summary>
    /// <param name="obj">Placeholder, useless</param>
    /// <param name="obj1">first element to compare</param>
    /// <param name="obj2">The second element to compare</param>
    /// <param name="result">If the return value is null, it proves that the two obj are not equal, an out int result needs to be returned, and the chain method call is truncated.</param>
    /// <param name="nullIsLast">Automatically put null in the last position</param>
    /// <returns></returns>
    public static object? CompareToObjDesc(this object obj, object? obj1, object? obj2, out int result,
        bool nullIsLast = true)
    {
        result = -1;
        if (CompareToNullObj(obj1, obj2, out var nullResult, nullIsLast))
        {
            result = nullResult;
            return null;
        }
        ArgumentNullException.ThrowIfNull(obj1);
        result = GetComparable(obj1).CompareTo(obj2) * result;
        return result != 0 ? null : new object();
    }

    /// <summary>
    /// Compare two objects in ascending order, which is used for quick implementation of Compare related methods. It supports null comparison and chain comparison.
    /// (If they are not equal, return null (the comparison value will be returned only when the return value is null), truncate the method call, and the return value is the comparison result. Otherwise, you can chain execution until the two are not equal and then compare the next weight) (need to implement the IComparable interface)
    /// </summary>
    /// <param name="obj">Placeholder, useless</param>
    /// <param name="obj1">first element to compare</param>
    /// <param name="obj2">The second element to compare</param>
    /// <param name="result">If the return value is null, it proves that the two obj are not equal, an out int result needs to be returned, and the chain method call is truncated.</param>
    /// <param name="nullIsLast">Automatically put null in the last position</param>
    /// <returns></returns>
    public static object? CompareToObjAsc(this object obj, object? obj1, object? obj2, out int result,
        bool nullIsLast = true)
    {
        result = 1;
        if (CompareToNullObj(obj1, obj2, out var nullResult, nullIsLast))
        {
            result = nullResult;
            return null;
        }
        ArgumentNullException.ThrowIfNull(obj1);
        result = GetComparable(obj1).CompareTo(obj2) * result;
        return result != 0 ? null : new object();
    }
    /// <summary>
    /// A shortcut method to implement comparator Comparison, supporting null comparison (need to implement IComparable interface)
    /// </summary>
    /// <param name="obj1"></param>
    /// <param name="obj2"></param>
    /// <param name="isDesc">Is it in descending order?</param>
    /// <param name="nullIsLast">Automatically put null in the last position</param>
    /// <returns></returns>
    public static int CompareToObj(this object? obj1, object? obj2, bool isDesc = false,
        bool nullIsLast = true)
    {
        var result = isDesc ? -1 : 1;
        if (CompareToNullObj(obj1, obj2, out var nullReturnValue, nullIsLast)) return nullReturnValue;
        ArgumentNullException.ThrowIfNull(obj1);
        return GetComparable(obj1).CompareTo(obj2) * result;
    }


    /// <summary>
    /// Comparison in Sort comparator is a shortcut method for comparing null. If any one is null, return true. In this case, out int needs to be returned as the Compare result.
    /// </summary>
    /// <param name="obj1"></param>
    /// <param name="obj2"></param>
    /// <param name="nullIsLast">By default, null is placed last.</param>
    /// <param name="returnValue"></param>
    /// <returns></returns>
    [ContractAnnotation("obj1:null => true; obj2:null => true")]
    private static bool CompareToNullObj(object? obj1, object? obj2, out int returnValue, bool nullIsLast = true)
    {
        returnValue = nullIsLast ? 1 : -1;
        if (obj1 == null && obj2 != null)
        {
            returnValue *= 1;
            return true;
        }

        if (obj1 != null && obj2 == null)
        {
            returnValue *= -1;
            return true;
        }

        if (obj1 == null)
        {
            returnValue = 0;
            return true;
        }

        return false;
    }

    private static IComparable GetComparable(object value)
        => value as IComparable
           ?? throw new InvalidCastException($"Type {value.GetType().FullName} does not implement {nameof(IComparable)}.");
}

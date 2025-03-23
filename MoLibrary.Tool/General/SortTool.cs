using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// 排序工具
    /// </summary>
    public static class SortTool
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
        /// 两个对象进行降序比较，用于Compare相关方法的快捷实现，支持null比较，支持链式比较
        /// （如果不相等返回null（判断返回为null才返回比较值），截断方法调用，返回值为比较结果。否则可以链式执行直到两者不相等后进行下一个权重的比较）（需要实现IComparable接口）
        /// </summary>
        /// <param name="obj">占位符，没什么用</param>
        /// <param name="obj1">比较的第一个元素</param>
        /// <param name="obj2">比较的第二个元素</param>
        /// <param name="result">如果返回值为null，则证明两个obj不相等，需要返回out int结果，链式方法调用被截断。</param>
        /// <param name="nullIsLast">自动将null放到最后一位</param>
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
            result = ((IComparable)obj1).CompareTo(obj2) * result;
            return result != 0 ? null : new object();
        }

        /// <summary>
        /// 两个对象进行升序比较，用于Compare相关方法的快捷实现，支持null比较，支持链式比较
        /// （如果不相等返回null（判断返回为null才返回比较值），截断方法调用，返回值为比较结果。否则可以链式执行直到两者不相等后进行下一个权重的比较）（需要实现IComparable接口）
        /// </summary>
        /// <param name="obj">占位符，没什么用</param>
        /// <param name="obj1">比较的第一个元素</param>
        /// <param name="obj2">比较的第二个元素</param>
        /// <param name="result">如果返回值为null，则证明两个obj不相等，需要返回out int结果，链式方法调用被截断。</param>
        /// <param name="nullIsLast">自动将null放到最后一位</param>
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
            result = ((IComparable)obj1).CompareTo(obj2) * result;
            return result != 0 ? null : new object();
        }
        /// <summary>
        /// 实现比较器Comparison的快捷方法，支持null比较（需要实现IComparable接口）
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <param name="isDesc">是否是降序</param>
        /// <param name="nullIsLast">自动将null放到最后一位</param>
        /// <returns></returns>
        public static int CompareToObj(this object? obj1, object? obj2, bool isDesc = false,
            bool nullIsLast = true)
        {
            var result = isDesc ? -1 : 1;
            if (CompareToNullObj(obj1, obj2, out var nullReturnValue, nullIsLast)) return nullReturnValue;
            return ((IComparable)obj1).CompareTo(obj2) * result;
        }


        /// <summary>
        /// Sort比较器中Comparison比较null的快捷方法。如果任意一个为null，返回true，此时需要返回out int为Compare结果
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <param name="nullIsLast">默认将null放到后一位</param>
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
    }
}
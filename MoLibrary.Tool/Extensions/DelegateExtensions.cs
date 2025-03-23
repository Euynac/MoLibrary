using System;
using System.Linq;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of Delegate related type
    /// </summary>
    public static class DelegateExtensions
    {
        /// <summary>
        /// 判断一个方法是否有任意一个元素满足。
        /// <br/>English: Determine whether a method satisfies any element.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="objects">当元素有为null时，必不满足</param>
        /// <returns></returns>
        public static bool SatisfyAny<T>(Func<T, bool>? predicate, params T[] objects)
        {
            return predicate != null && objects.Any(predicate);
        }
        /// <summary>
        /// 判断一个方法是否所有元素都满足
        /// <br/>English: Determine whether a method satisfies all elements.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="objects">当元素有为null时，必不满足</param>
        /// <returns></returns>
        public static bool SatisfyAll<T>(Func<T, bool>? predicate, params T[] objects)
        {
            return predicate != null && objects.All(predicate);
        }
    }
}
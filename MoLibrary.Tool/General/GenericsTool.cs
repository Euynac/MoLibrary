using System;
using System.Linq;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// 泛型工具类
    /// </summary>
    public static class GenericsTool
    {
        /// <summary>
        /// 判断当前类型是否实现了给定的泛型类型（比如IList&lt;&gt;之类）
        /// </summary>
        /// <param name="type"></param>
        /// <param name="genericType">需要使用typeof(IList&lt;&gt;)</param>
        /// <returns></returns>
        public static bool ImplementsGenericType(this Type type, Type genericType) => type.GetInterfaces()
            .Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == genericType);
        /// <summary>
        /// 将Predicate转化为对应的Func
        /// </summary>
        /// <param name="predicate"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Func<T, bool> PredicateConvertToFunc<T>(Predicate<T> predicate) => new(predicate);


        /// <summary>
        /// 将Func第一个、第二个参数类型（TIn）支持协变，即将TIn转换为指定类型TOut（TIn需是TOut的子类）
        /// </summary>
        /// <typeparam name="TIn1"></typeparam>
        /// <typeparam name="TOut2"></typeparam>
        /// <typeparam name="TR"></typeparam>
        /// <typeparam name="TOut1"></typeparam>
        /// <typeparam name="TIn2"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Func<TOut1, TOut2, TR> ConvertFunc<TIn1, TOut1, TIn2, TOut2, TR>(this Func<TIn1, TIn2, TR> func)
            where TIn2 : TOut2
            where TIn1 : TOut1
        {
            return (t, p) => func((TIn1)t, (TIn2)p);
        }

        /// <summary>
        /// (in TIn, out TR)类型 将Func第一个参数类型（TIn）支持协变，即将TIn转换为指定类型TOut（TIn需是TOut的子类）
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <typeparam name="TR"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Func<TOut, TR> ConvertFunc<TIn, TOut, TR>(this Func<TIn, TR> func) where TIn : TOut
        {
            return p => func((TIn)p);
        }
    }
}

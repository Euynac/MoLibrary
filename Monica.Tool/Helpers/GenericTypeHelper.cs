namespace Monica.Tool.Helpers;

/// <summary>
/// Provides helpers for inspecting and adapting generic type contracts.
/// </summary>
public static class GenericTypeHelper
{
    /// <summary>
    /// Determine whether the current type implements the given generic type (such as IList&lt;&gt;, etc.)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="genericType">Need to use typeof(IList&lt;&gt;)</param>
    /// <returns></returns>
    public static bool ImplementsGenericType(this Type type, Type genericType)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(genericType);

        return ReflectionHelper.IsAssignableToGenericType(type, genericType);
    }
    /// <summary>
    /// Convert Predicate into corresponding Func
    /// </summary>
    /// <param name="predicate"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static Func<T, bool> PredicateConvertToFunc<T>(Predicate<T> predicate) => new(predicate);


    /// <summary>
    /// Support covariance for the first and second parameter types (TIn) of Func, that is, convert TIn to the specified type TOut (TIn needs to be a subclass of TOut)
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
        return (t, p) => func((TIn1)t!, (TIn2)p!);
    }

    /// <summary>
    /// (in TIn, out TR) type The first parameter type of Func (TIn) supports covariance, that is, converts TIn to the specified type TOut (TIn needs to be a subclass of TOut)
    /// </summary>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <typeparam name="TR"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<TOut, TR> ConvertFunc<TIn, TOut, TR>(this Func<TIn, TR> func) where TIn : TOut
    {
        return p => func((TIn)p!);
    }
}

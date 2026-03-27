namespace Monica.Tool.Extensions;

/// <summary>
/// Extension methods of Delegate related type
/// </summary>
public static class DelegateExtensions
{
    /// <summary>
    /// Determine whether any element of a method is satisfied.
    /// <br/>English: Determine whether a method satisfies any element.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="predicate"></param>
    /// <param name="objects">When the element is null, it will not be satisfied.</param>
    /// <returns></returns>
    public static bool SatisfyAny<T>(Func<T, bool>? predicate, params T[] objects)
    {
        return predicate != null && objects.Any(predicate);
    }
    /// <summary>
    /// Determine whether all elements of a method are satisfied
    /// <br/>English: Determine whether a method satisfies all elements.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="predicate"></param>
    /// <param name="objects">When the element is null, it will not be satisfied.</param>
    /// <returns></returns>
    public static bool SatisfyAll<T>(Func<T, bool>? predicate, params T[] objects)
    {
        return predicate != null && objects.All(predicate);
    }
}
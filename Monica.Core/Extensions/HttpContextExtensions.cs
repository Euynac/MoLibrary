using Microsoft.AspNetCore.Http;

namespace Monica.Core.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Gets a request-scoped shared object from <see cref="HttpContext.Items"/>,
    /// or stores and returns the provided default value when the entry does not exist.
    /// </summary>
    /// <returns>The existing shared object or <paramref name="defaultValue"/>.</returns>
    public static T? GetOrDefault<T>(this HttpContext context, T? defaultValue = default) where T : class
    {
        if (context.Items.TryGetValue(typeof(T).Name, out var valueObject) && valueObject is T value)
        {
            return value;
        }

        context.Items[typeof(T).Name] = defaultValue;
        return defaultValue;
    }

    /// <summary>
    /// Gets a request-scoped shared object from <see cref="HttpContext.Items"/>,
    /// or creates and stores a new instance when none exists.
    /// </summary>
    /// <returns>The existing or newly created shared object.</returns>
    public static T GetOrNew<T>(this HttpContext context) where T : class, new()
    {
        return GetOrDefault(context, new T())!;
    }

    /// <summary>
    /// Stores a request-scoped shared object in <see cref="HttpContext.Items"/>.
    /// </summary>
    /// <typeparam name="T">The shared object type.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="value">The value to store.</param>
    public static void Set<T>(this HttpContext context, T value) where T : class
    {
        context.Items[typeof(T).Name] = value;
    }
}

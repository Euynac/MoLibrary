using Microsoft.AspNetCore.Http;

namespace BuildingBlocksPlatform.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// 获取或获得默认当前Http请求上下文共享对象
    /// </summary>
    /// <returns></returns>
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
    /// 获取或新建当前Http请求上下文共享对象
    /// </summary>
    /// <returns></returns>
    public static T GetOrNew<T>(this HttpContext context) where T : class, new()
    {
        return GetOrDefault(context, new T())!;
    }

    /// <summary>
    /// 设置当前Http请求上下文共享对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="context"></param>
    /// <param name="value"></param>
    public static void Set<T>(this HttpContext context, T value) where T : class
    {
        context.Items[typeof(T).Name] = value;
    }
}
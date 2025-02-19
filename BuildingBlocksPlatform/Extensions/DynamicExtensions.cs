using BuildingBlocksPlatform.SeedWork;
using System.Dynamic;

namespace BuildingBlocksPlatform.Extensions;

public static class DynamicExtensions
{
    public static bool Exist(this ExpandoObject obj, string name)
    {
        var dict = (IDictionary<string, object>) obj!;
        return dict.ContainsKey(name);
    }
    public static void Set(this ExpandoObject obj, string name, object? info)
    {
        var dict = (IDictionary<string, object>) obj!;
        dict[name] = info ?? "<null>";
    }
    public static object? GetOrDefault(this ExpandoObject obj, string name, object? defaultValue = default)
    {
        var dict = (IDictionary<string, object>) obj!;
        if (dict.TryGetValue(name, out var data)) return data;
        return defaultValue;
    }
    public static void Copy(this ExpandoObject obj, ExpandoObject copyFrom)
    {
        var dict = (IDictionary<string, object>) obj!;
        var from = (IDictionary<string, object>) copyFrom!;
        foreach (var o in from)
        {
            dict[o.Key] = o.Value;
        }
    }
    public static void Append(this ExpandoObject obj, string name, object? info)
    {
        var dict = (IDictionary<string, object>) obj!;
        var index = 0;
        var finalKey = name;
        while (dict.ContainsKey(finalKey))
        {
            finalKey = $"{name}_{++index}";
        }
        dict[finalKey] = info ?? "<null>";
    }
}
using System.Collections.Generic;
using System.Dynamic;

namespace MoLibrary.Tool.Extensions;

public static class DynamicExtensions
{
    public static IDictionary<string, object> Unfold(this ExpandoObject obj)
    {
        return obj!;
    }

    public static bool Exist(this ExpandoObject obj, string name)
    {
        IDictionary<string, object> dict = obj!;
        return dict.ContainsKey(name);
    }
    public static void Set(this ExpandoObject obj, string name, object? info)
    {
        IDictionary<string, object> dict = obj!;
        dict[name] = info ?? "<null>";
    }
    public static object? GetOrDefault(this ExpandoObject obj, string name, object? defaultValue = null)
    {
        IDictionary<string, object> dict = obj!;
        return dict.TryGetValue(name, out var data) ? data : defaultValue;
    }
    public static void Copy(this ExpandoObject obj, ExpandoObject copyFrom)
    {
        IDictionary<string, object> dict = obj!;
        IDictionary<string, object> from = copyFrom!;
        foreach (var o in from)
        {
            dict[o.Key] = o.Value;
        }
    }
    public static void Merge(this ExpandoObject obj, ExpandoObject copyFrom)
    {
        IDictionary<string, object> from = copyFrom!;
        foreach (var o in from)
        {
            obj.Append(o.Key, o.Value);
        }
    }
    public static void Append(this ExpandoObject obj, string name, object? info)
    {
        IDictionary<string, object> dict = obj!;
        var index = 0;
        var finalKey = name;
        while (dict.ContainsKey(finalKey))
        {
            finalKey = $"{name}_{++index}";
        }
        dict[finalKey] = info ?? "<null>";
    }
}
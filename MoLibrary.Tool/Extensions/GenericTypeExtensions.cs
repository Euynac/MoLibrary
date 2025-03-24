using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MoLibrary.Tool.Extensions;


public static class GenericTypeExtensions
{
    /// <summary>
    /// 一般用于泛型类型的日志格式化
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string GetGenericTypeName(this Type type)
    {
        string typeName;

        if (type.IsGenericType)
        {
            var genericTypes = string.Join(",", type.GetGenericArguments().Select(t => t.Name).ToArray());
            typeName = $"{type.Name.Remove(type.Name.IndexOf('`'))}<{genericTypes}>";
        }
        else
        {
            typeName = type.Name;
        }

        return typeName;
    }

    public static string GetGenericTypeName(this object @object)
    {
        return @object.GetType().GetGenericTypeName();
    }

    public static bool IsCollectionType(this Type type)
    {
        return type.IsGenericType && type.GetInterfaces()
            .Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));
    }

    public static bool IsDictionary(this Type type)
    {
        //or typeof(IDictionary).IsAssignableFrom(type);
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
    }

    /// <summary>
    /// Get the underlying type of generic T in such as type of IEnumerable&lt;T&gt; and T?.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Type GetGenericUnderlyingType(this Type type)
    {
        Type? underlyingType = null;
        if (!type.IsGenericType) return type;
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            if (type.IsArray)
            {
                underlyingType = type.GetElementType() ?? throw new Exception($"{type.FullName} not support in method {nameof(GetGenericUnderlyingType)}");
            }
            else
            {
                underlyingType = type.GetGenericArguments().FirstOrDefault() ?? throw new Exception($"{type.FullName} not support in method {nameof(GetGenericUnderlyingType)}");
            }
                
        }

        underlyingType ??= type;
        if (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof (Nullable<>))
        {
            underlyingType = Nullable.GetUnderlyingType(type);
        }

        return underlyingType ?? type;
    }
}
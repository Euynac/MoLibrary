using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MoLibrary.Tool.Extensions;


public static class GenericTypeExtensions
{

    /// <summary>
    /// 获取类型的完整命名空间路径名称，处理泛型、嵌套类和匿名类型，一般用于日志格式化
    /// </summary>
    public static string GetCleanFullName(this Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        return InnerGetCleanName(type);
        string InnerGetCleanName(Type curType, bool jumpDeclareType = false, bool getFullName = true)
        {
            // 处理嵌套类（优先级最高） 类型名称中的 + 符号表示 ​嵌套类（Nested Class）​。
            if (curType.DeclaringType != null && !jumpDeclareType)
            {
                var parentName = curType.DeclaringType.GetCleanFullName();
                var currentName = InnerGetCleanName(curType, true, false);
                return $"{parentName}.{currentName}";
            }

            // 处理匿名类型
            if (curType.Name.Contains("AnonymousType", StringComparison.Ordinal))
            {
                var signature = string.Join("-", curType.GetProperties()
                    .Select(p => $"{p.Name}[{p.PropertyType.GetCleanFullName()}]"));
                return $"{curType.Assembly.GetName().Name}:AnonymousType_{signature}";
            }

            // 处理泛型类型
            if (curType.IsGenericType)
            {
                var genericTypeDef = curType.GetGenericTypeDefinition();
                var genericArgs = curType.GetGenericArguments().Select(t => InnerGetCleanName(t, true)).ToArray();

                // 构造完整类型名称
                var typeName = getFullName ? genericTypeDef.FullName ?? genericTypeDef.Name : genericTypeDef.Name;
                var index = typeName.IndexOf('`');
                if (index != -1) typeName = typeName[..index];
                return typeName + $"<{string.Join(",", genericArgs)}>";
            }

            return getFullName ? curType.FullName ?? curType.Name : curType.Name;
        }
    }

    public static string GetCleanFullName(this object @object)
    {
        return @object.GetType().GetCleanFullName();
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
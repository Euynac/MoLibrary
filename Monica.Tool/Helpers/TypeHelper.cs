using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Monica.Tool.Extensions;

namespace Monica.Tool.Helpers;

public static class TypeHelper
{
    private static readonly HashSet<Type> FloatingTypes =
    [
        typeof(float),
        typeof(double),
        typeof(decimal)
    ];

    private static readonly HashSet<Type> NonNullablePrimitiveTypes =
    [
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(sbyte),
        typeof(ushort),
        typeof(uint),
        typeof(ulong),
        typeof(bool),
        typeof(char),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(IntPtr),
        typeof(UIntPtr)
    ];

    public static bool IsNonNullablePrimitiveType(Type type)
    {
        return NonNullablePrimitiveTypes.Contains(type);
    }

    public static bool IsFunc(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        var type = obj.GetType();
        if (!type.GetTypeInfo().IsGenericType || !typeof(Delegate).IsAssignableFrom(type))
        {
            return false;
        }

        var genericDefinition = type.GetGenericTypeDefinition();
        return genericDefinition.Namespace == typeof(Func<>).Namespace &&
               genericDefinition.Name.StartsWith("Func`", StringComparison.Ordinal);
    }

    public static bool IsFunc<TReturn>(object? obj)
    {
        return IsFunc(obj) && obj!.GetType().GenericTypeArguments[^1] == typeof(TReturn);
    }

    public static bool IsPrimitiveExtended(Type type, bool includeNullables = true, bool includeEnums = false)
    {
        if (IsPrimitiveExtendedInternal(type, includeEnums))
        {
            return true;
        }

        if (includeNullables && type.IsNullableValueType() && type.GenericTypeArguments.Any())
        {
            return IsPrimitiveExtendedInternal(type.GenericTypeArguments[0], includeEnums);
        }

        return false;
    }

    public static bool IsNullable(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Nullable.GetUnderlyingType(type) != null;
    }

    public static Type GetFirstGenericArgumentIfNullable(this Type t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return Nullable.GetUnderlyingType(t) ?? t;
    }

    public static bool IsEnumerable(Type type, out Type? itemType, bool includePrimitives = true)
    {
        if (!includePrimitives && IsPrimitiveExtended(type))
        {
            itemType = null;
            return false;
        }

        var enumerableTypes = ReflectionHelper.GetImplementedGenericTypes(type, typeof(IEnumerable<>));
        if (enumerableTypes.Count == 1)
        {
            itemType = enumerableTypes[0].GenericTypeArguments[0];
            return true;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            itemType = typeof(object);
            return true;
        }

        itemType = null;
        return false;
    }

    public static bool IsDictionary(Type type, out Type? keyType, out Type? valueType)
    {
        var dictionaryTypes = ReflectionHelper
            .GetImplementedGenericTypes(
                type,
                typeof(IDictionary<,>)
            );

        if (dictionaryTypes.Count == 1)
        {
            keyType = dictionaryTypes[0].GenericTypeArguments[0];
            valueType = dictionaryTypes[0].GenericTypeArguments[1];
            return true;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            keyType = typeof(object);
            valueType = typeof(object);
            return true;
        }

        keyType = null;
        valueType = null;

        return false;
    }

    private static bool IsPrimitiveExtendedInternal(Type type, bool includeEnums)
    {
        if (type.IsPrimitive)
        {
            return true;
        }

        if (includeEnums && type.IsEnum)
        {
            return true;
        }

        return type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }

    public static T? GetDefaultValue<T>()
    {
        return default;
    }

    public static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        return null;
    }

    public static string GetFullNameHandlingNullableAndGenerics(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return type.GenericTypeArguments[0].FullName + "?";
        }

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            var genericTypeFullName = genericType.FullName!;
            return
                $"{genericTypeFullName.Left(genericTypeFullName.IndexOf('`'))}<{type.GenericTypeArguments.Select(GetFullNameHandlingNullableAndGenerics).JoinAsString(",")}>";
        }

        return type.FullName ?? type.Name;
    }

    public static string GetSimplifiedName(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return GetSimplifiedName(type.GenericTypeArguments[0]) + "?";
        }

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            var genericTypeFullName = genericType.FullName!;
            return
                $"{genericTypeFullName.Left(genericTypeFullName.IndexOf('`'))}<{type.GenericTypeArguments.Select(GetSimplifiedName).JoinAsString(",")}>";
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(char))
        {
            return "string";
        }

        if (type.IsNumeric()) return "number";

        if (type == typeof(DateTime))
        {
            return "string";
        }

        if (type == typeof(DateTimeOffset))
        {
            return "string";
        }

        if (type.FullName == "System.DateOnly")
        {
            return "string";
        }

        if (type.FullName == "System.TimeOnly")
        {
            return "string";
        }

        if (type == typeof(TimeSpan))
        {
            return "string";
        }

        if (type == typeof(Guid))
        {
            return "string";
        }

        if (type == typeof(IntPtr))
        {
            return "number";
        }

        if (type == typeof(UIntPtr))
        {
            return "number";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        return type.FullName ?? type.Name;
    }


    public static bool IsFloatingType(Type type, bool includeNullable = true)
    {
        if (FloatingTypes.Contains(type))
        {
            return true;
        }

        if (includeNullable &&
            type.IsNullableValueType() &&
            FloatingTypes.Contains(type.GenericTypeArguments[0]))
        {
            return true;
        }

        return false;
    }

    public static object ConvertFrom<TTargetType>(object value)
    {
        return ConvertFrom(typeof(TTargetType), value);
    }

    public static object ConvertFrom(Type targetType, object value)
    {
        return TypeDescriptor
            .GetConverter(targetType)
            .ConvertFrom(value)!;
    }


    public static bool IsDefaultValue(object? obj)
    {
        if (obj == null)
        {
            return true;
        }

        return obj.Equals(GetDefaultValue(obj.GetType()));
    }
}

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MoLibrary.Tool.Extensions;

public static class SystemTypeExtensions
{
    /// <summary>
    /// Determines whether an instance of this type can be assigned to
    /// an instance of the <typeparamref name="TTarget"></typeparamref>.
    ///
    /// Internally uses <see cref="Type.IsAssignableFrom"/>.
    /// </summary>
    /// <typeparam name="TTarget">Target type</typeparam> (as reverse).
    public static bool IsAssignableTo<TTarget>(this Type type)
    {
        return type.IsAssignableTo(typeof(TTarget));
    }
}

public static class TypeExtensions
{
    public static string GetFullNameWithAssemblyName(this Type type)
    {
        return type.FullName + ", " + type.Assembly.GetName().Name;
    }

    /// <summary>
    /// 判断是否能够使用无参构造函数创建该类型的实例
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool CanCreateInstanceUsingParameterlessConstructor(this Type type)
    {
        return type.IsValueType || !type.IsAbstract &&
            type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                Type.EmptyTypes, null) != null;//包括private classes
    }


    /// <summary>
    /// 判断一个类是引用类型，且不是string
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsClassObject(this Type type)
    {
        return type.IsClass && type != typeof(string);
    }

    /// <summary>
    /// Judge whether the type has explicit defined static constructor
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool HasExplicitDefinedStaticConstructor(this Type type)
    {
        //https://stackoverflow.com/questions/74134653/check-if-a-class-has-an-explicit-static-constructor
        return type.TypeInitializer is { } initializer && !type.Attributes.HasFlag(TypeAttributes.BeforeFieldInit);
    }

    /// <summary>
    /// The same as <see cref="RuntimeHelpers.RunClassConstructor"/>
    /// </summary>
    /// <param name="type"></param>
    public static void RunStaticConstructor(this Type type)
    {
        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }

    /// <summary>
    /// 判断该类是否实现了指定接口类型
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsImplementInterface<T>(this Type type) where T : class
    {
        return IsImplementInterface(type, typeof(T));
    }

    /// <summary>
    /// 判断该类是否实现了指定接口类型
    /// </summary>
    /// <param name="type"></param>
    /// <param name="interfaceType"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsImplementInterface(this Type type, Type interfaceType)
    {
        if (!interfaceType.IsInterface)
            throw new InvalidOperationException($"{interfaceType.FullName} is not a interface type!");
        return interfaceType.IsAssignableFrom(type);

        //mode 2 but slower?
        //return type.GetInterfaces().Contains(interfaceType);
    }

    /// <summary>
    /// 是否被标记为可空值类型（.NET6后）
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <returns></returns>
    public static bool IsMarkedAsNullable(this PropertyInfo propertyInfo)
    {
        return new NullabilityInfoContext().Create(propertyInfo).ReadState is NullabilityState.Nullable;
    }

    /// <summary>
    /// test something like typeof(IMyInterface&lt;&gt;)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="interfaceGenericType"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static bool IsImplementInterfaceGeneric(this Type type, Type interfaceGenericType)
    {
        if (!interfaceGenericType.IsInterface)
            throw new InvalidOperationException($"{interfaceGenericType.FullName} is not a interface type!");
        return Array.Exists(type.GetInterfaces(),
            i => i.IsGenericType && interfaceGenericType == i.GetGenericTypeDefinition());
    }

    /// <summary>
    /// test something like typeof(IMyInterface&lt;&gt;)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="interfaceGenericType"></param>
    /// <param name="interfaceWithExactGenericType"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static bool IsImplementInterfaceGeneric(this Type type, Type interfaceGenericType, [NotNullWhen(true)]out Type? interfaceWithExactGenericType)
    {
        if (!interfaceGenericType.IsInterface)
            throw new InvalidOperationException($"{interfaceGenericType.FullName} is not a interface type!");
        interfaceWithExactGenericType = null;
        foreach (var i in type.GetInterfaces())
        {
            if (i.IsGenericType && interfaceGenericType == i.GetGenericTypeDefinition())
            {
                interfaceWithExactGenericType = i;
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Test something like typeof(MyGenericClass&lt;&gt;)
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsSubclassOfRawGeneric<TGeneric>(this Type type) where TGeneric:class
    {
        return IsSubclassOfRawGeneric(type, typeof(TGeneric));
    }
    /// <summary>
    /// Test something like typeof(MyGenericClass&lt;&gt;)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="genericType"></param>
    /// <returns></returns>
    public static bool IsSubclassOfRawGeneric(this Type type, Type genericType)
    {
        return IsSubclassOfRawGeneric(type, genericType, out _);
    }

    /// <summary>
    /// Test something like typeof(MyGenericClass&lt;&gt;)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="genericType"></param>
    /// <param name="interfaceWithExactGenericType"></param>
    /// <returns></returns>
    public static bool IsSubclassOfRawGeneric(this Type type, Type genericType, [NotNullWhen(true)] out Type? interfaceWithExactGenericType)
    {
        interfaceWithExactGenericType = null;
        var check = type;
        while (check != null && check != typeof(object))
        {
            if (check.IsGenericType && check.GetGenericTypeDefinition() == genericType)
            {
                interfaceWithExactGenericType = check;
                return true;
            }
            check = check.BaseType;
        }

        return false;
    }
    /// <summary>
    /// Judge whether the method is override the base class's virtual method.
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static bool IsOverride(this MethodInfo methodInfo)
    {
        return methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
    }
    /// <summary>
    /// Judge whether the specific type is nullable value type or not.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsNullableValueType(this Type type) => type is { IsGenericType: true, IsGenericTypeDefinition: false } &&
                                                              type.GetGenericTypeDefinition() == typeof(Nullable<>);
    /// <summary>
    /// Judge whether the specific type's instance can be null or not.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool CanBeNull(this Type type)
    {
        return !type.GetTypeInfo().IsValueType || IsNullableValueType(type);
    }
    /// <summary>
    /// Judge whether the specific type is derived from giving generic type or not.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="genericType">typeof(GenericType&lt;&gt;)</param>
    /// <returns></returns>
    public static bool IsDerivedFromGenericType(this Type type, Type genericType) =>
        type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == genericType;

    /// <summary>
    /// Get the underlying type of nullable type.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Type StripNullable(this Type type)
    {
        //similar official method: Nullable.GetUnderlyingType()
        return !IsNullableValueType(type) ? type : type.GenericTypeArguments[0];
    }

    /// <summary>
    /// Get the underlying type T in such as type of IEnumerable&lt;T&gt; and T?
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Type GetUnderlyingType(this Type type)
    {
        Type? underlyingType = null;
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            if (type.IsArray)
            {
                underlyingType = type.GetElementType() ?? throw new Exception($"{type.FullName} not support {nameof(GetUnderlyingType)}");
            }
            else
            {
                underlyingType = type.GetGenericArguments().FirstOrDefault() ?? throw new Exception($"{type.FullName} not support {nameof(GetUnderlyingType)}");
            }

        }

        underlyingType ??= type;
        if (underlyingType.IsNullableValueType())
        {
            underlyingType = Nullable.GetUnderlyingType(type);
        }

        return underlyingType ?? type;
    }
}
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
    /// <summary>
    /// Gets the full name of the type combined with its assembly name in a format suitable for type loading.
    /// This method provides a string representation that can be used with Type.GetType() for dynamic type loading.
    /// </summary>
    /// <param name="type">The type to get the assembly-qualified name for</param>
    /// <returns>A string containing the type's full name and assembly name</returns>
    public static string GetFullNameWithAssemblyName(this Type type)
    {
        return type.FullName + ", " + type.Assembly.GetName().Name;
    }

    /// <summary>
    /// Determines whether an instance of the specified type can be instantiated using a parameterless constructor.
    /// This includes value types (which always have implicit parameterless constructors) and reference types
    /// with explicitly defined or implicit parameterless constructors (including private ones).
    /// </summary>
    /// <param name="type">The type to check for parameterless constructor availability</param>
    /// <returns>True if the type can be instantiated without parameters; otherwise, false</returns>
    public static bool CanCreateInstanceUsingParameterlessConstructor(this Type type)
    {
        return type.IsValueType || !type.IsAbstract &&
            type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                Type.EmptyTypes, null) != null; // Including private classes
    }


    /// <summary>
    /// Determines whether the specified type represents a class object (reference type) excluding string.
    /// This is useful for distinguishing between complex objects and primitive/string types in reflection scenarios.
    /// </summary>
    /// <param name="type">The type to examine</param>
    /// <returns>True if the type is a class but not string; otherwise, false</returns>
    public static bool IsClassObject(this Type type)
    {
        return type.IsClass && type != typeof(string);
    }

    /// <summary>
    /// Determines whether the specified type has an explicitly defined static constructor.
    /// This method distinguishes between types with explicit static constructors and those
    /// that only have compiler-generated field initializers (BeforeFieldInit types).
    /// </summary>
    /// <param name="type">The type to check for explicit static constructor</param>
    /// <returns>True if the type has an explicitly defined static constructor; otherwise, false</returns>
    public static bool HasExplicitDefinedStaticConstructor(this Type type)
    {
        //https://stackoverflow.com/questions/74134653/check-if-a-class-has-an-explicit-static-constructor
        return type.TypeInitializer is { } initializer && !type.Attributes.HasFlag(TypeAttributes.BeforeFieldInit);
    }

    /// <summary>
    /// Forces the runtime to execute the static constructor for the specified type if it hasn't been executed yet.
    /// This is an extension method wrapper around <see cref="RuntimeHelpers.RunClassConstructor"/> for convenience.
    /// </summary>
    /// <param name="type">The type whose static constructor should be executed</param>
    public static void RunStaticConstructor(this Type type)
    {
        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }

    /// <summary>
    /// Determines whether the specified type implements the given interface type.
    /// This generic version provides compile-time type safety for interface checking.
    /// </summary>
    /// <typeparam name="T">The interface type to check for implementation (must be a reference type)</typeparam>
    /// <param name="type">The type to examine for interface implementation</param>
    /// <returns>True if the type implements the specified interface; otherwise, false</returns>
    public static bool IsImplementInterface<T>(this Type type) where T : class
    {
        return IsImplementInterface(type, typeof(T));
    }

    /// <summary>
    /// Determines whether the specified type implements the given interface type.
    /// This method uses <see cref="Type.IsAssignableFrom"/> for efficient interface checking.
    /// </summary>
    /// <param name="type">The type to examine for interface implementation</param>
    /// <param name="interfaceType">The interface type to check for</param>
    /// <returns>True if the type implements the specified interface; otherwise, false</returns>
    /// <exception cref="InvalidOperationException">Thrown when interfaceType is not an interface</exception>
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsImplementInterface(this Type type, Type interfaceType)
    {
        if (!interfaceType.IsInterface)
            throw new InvalidOperationException($"{interfaceType.FullName} is not a interface type!");
        return interfaceType.IsAssignableFrom(type);

        // Mode 2 but slower?
        // return type.GetInterfaces().Contains(interfaceType);
    }

    /// <summary>
    /// Determines whether the specified property is marked as nullable using nullable reference types (.NET 6+).
    /// This method uses the NullabilityInfoContext to analyze the property's nullability annotations.
    /// </summary>
    /// <param name="propertyInfo">The property to check for nullable annotation</param>
    /// <returns>True if the property is marked as nullable; otherwise, false</returns>
    public static bool IsMarkedAsNullable(this PropertyInfo propertyInfo)
    {
        return new NullabilityInfoContext().Create(propertyInfo).ReadState is NullabilityState.Nullable;
    }

    /// <summary>
    /// Determines whether the specified type implements a generic interface with the given generic type definition,
    /// or if the type itself is the specified generic interface. This method can check for implementations of 
    /// open generic interfaces like IEnumerable&lt;&gt; or IRepository&lt;&gt;.
    /// </summary>
    /// <param name="type">The type to examine for generic interface implementation</param>
    /// <param name="interfaceGenericType">The generic interface type definition (e.g., typeof(IMyInterface&lt;&gt;))</param>
    /// <returns>True if the type implements the generic interface; otherwise, false</returns>
    /// <exception cref="InvalidOperationException">Thrown when interfaceGenericType is not an interface</exception>
    public static bool IsImplementInterfaceGeneric(this Type type, Type interfaceGenericType)
    {
        if (!interfaceGenericType.IsInterface)
            throw new InvalidOperationException($"{interfaceGenericType.FullName} is not a interface type!");
        
        // Check if the type itself is the interface
        if (type is { IsInterface: true, IsGenericType: true} && interfaceGenericType == type.GetGenericTypeDefinition())
            return true;
            
        return Array.Exists(type.GetInterfaces(),
            i => i.IsGenericType && interfaceGenericType == i.GetGenericTypeDefinition());
    }

    /// <summary>
    /// Determines whether the specified type implements a generic interface (or is the interface itself) and returns the exact generic type implementation.
    /// This overload provides access to the concrete generic interface type with its specific type arguments.
    /// </summary>
    /// <param name="type">The type to examine for generic interface implementation</param>
    /// <param name="interfaceGenericType">The generic interface type definition (e.g., typeof(IMyInterface&lt;&gt;))</param>
    /// <param name="exactGenericTypeDefinition">When this method returns true, contains the exact generic interface implementation</param>
    /// <returns>True if the type implements the generic interface; otherwise, false</returns>
    /// <exception cref="InvalidOperationException">Thrown when interfaceGenericType is not an interface</exception>
    public static bool IsImplementInterfaceGeneric(this Type type, Type interfaceGenericType, [NotNullWhen(true)]out Type? exactGenericTypeDefinition)
    {
        if (!interfaceGenericType.IsInterface)
            throw new InvalidOperationException($"{interfaceGenericType.FullName} is not a interface type!");
        exactGenericTypeDefinition = null;
        
        // Check if the type itself is the interface
        if (type is { IsInterface: true, IsGenericType: true} && interfaceGenericType == type.GetGenericTypeDefinition())
        {
            exactGenericTypeDefinition = type;
            return true;
        }
        
        foreach (var i in type.GetInterfaces())
        {
            if (i.IsGenericType && interfaceGenericType == i.GetGenericTypeDefinition())
            {
                exactGenericTypeDefinition = i;
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Determines whether the specified type is a subclass of the given raw generic type.
    /// This generic version provides compile-time type safety for generic inheritance checking.
    /// </summary>
    /// <typeparam name="TGeneric">The generic base class type to check for inheritance</typeparam>
    /// <param name="type">The type to examine for generic inheritance</param>
    /// <returns>True if the type inherits from the specified generic type; otherwise, false</returns>
    public static bool IsSubclassOfRawGeneric<TGeneric>(this Type type) where TGeneric:class
    {
        return IsSubclassOfRawGeneric(type, typeof(TGeneric));
    }
    /// <summary>
    /// Determines whether the specified type is a subclass of the given raw generic type definition.
    /// This method can check inheritance from open generic types like List&lt;&gt; or Repository&lt;&gt;.
    /// </summary>
    /// <param name="type">The type to examine for generic inheritance</param>
    /// <param name="genericType">The generic type definition to check for inheritance (e.g., typeof(MyGenericClass&lt;&gt;))</param>
    /// <returns>True if the type inherits from the specified generic type; otherwise, false</returns>
    public static bool IsSubclassOfRawGeneric(this Type type, Type genericType)
    {
        return IsSubclassOfRawGeneric(type, genericType, out _);
    }

    /// <summary>
    /// Determines whether the specified type is a subclass of the given raw generic type and returns the exact generic base class.
    /// This overload provides access to the concrete generic base class with its specific type arguments.
    /// </summary>
    /// <param name="type">The type to examine for generic inheritance</param>
    /// <param name="genericType">The generic type definition to check for inheritance (e.g., typeof(MyGenericClass&lt;&gt;))</param>
    /// <param name="exactGenericTypeDefinition">When this method returns true, contains the exact generic base class implementation</param>
    /// <returns>True if the type inherits from the specified generic type; otherwise, false</returns>
    public static bool IsSubclassOfRawGeneric(this Type type, Type genericType, [NotNullWhen(true)] out Type? exactGenericTypeDefinition)
    {
        exactGenericTypeDefinition = null;
        var check = type;
        while (check != null && check != typeof(object))
        {
            if (check.IsGenericType && check.GetGenericTypeDefinition() == genericType)
            {
                exactGenericTypeDefinition = check;
                return true;
            }
            check = check.BaseType;
        }

        return false;
    }
    /// <summary>
    /// Determines whether the specified method overrides a virtual method from its base class.
    /// This method compares the declaring type of the method with its base definition to detect overrides.
    /// </summary>
    /// <param name="methodInfo">The method to check for override behavior</param>
    /// <returns>True if the method overrides a base class virtual method; otherwise, false</returns>
    public static bool IsOverride(this MethodInfo methodInfo)
    {
        return methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
    }
    /// <summary>
    /// Determines whether the specified type is a nullable value type (Nullable&lt;T&gt;).
    /// This method specifically checks for the Nullable&lt;&gt; generic type wrapper around value types.
    /// </summary>
    /// <param name="type">The type to examine for nullable value type pattern</param>
    /// <returns>True if the type is Nullable&lt;T&gt; where T is a value type; otherwise, false</returns>
    public static bool IsNullableValueType(this Type type) => type is { IsGenericType: true, IsGenericTypeDefinition: false } &&
                                                              type.GetGenericTypeDefinition() == typeof(Nullable<>);
    /// <summary>
    /// Determines whether instances of the specified type can be assigned a null value.
    /// This includes all reference types and nullable value types (Nullable&lt;T&gt;).
    /// </summary>
    /// <param name="type">The type to examine for null assignability</param>
    /// <returns>True if instances of this type can be null; otherwise, false</returns>
    public static bool CanBeNull(this Type type)
    {
        return !type.GetTypeInfo().IsValueType || IsNullableValueType(type);
    }
    /// <summary>
    /// Determines whether the specified type is a direct instantiation of the given generic type definition.
    /// This method checks if the type itself (not its base classes) matches the generic type pattern.
    /// </summary>
    /// <param name="type">The type to examine for generic type matching</param>
    /// <param name="genericType">The generic type definition to match against (e.g., typeof(GenericType&lt;&gt;))</param>
    /// <returns>True if the type is a direct instantiation of the generic type; otherwise, false</returns>
    public static bool IsDerivedFromGenericType(this Type type, Type genericType) =>
        type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == genericType;

    /// <summary>
    /// Gets the underlying value type from a nullable value type, or returns the original type if not nullable.
    /// This method unwraps Nullable&lt;T&gt; types to reveal the underlying value type T.
    /// </summary>
    /// <param name="type">The type to strip nullable wrapper from</param>
    /// <returns>The underlying value type if nullable; otherwise, the original type</returns>
    public static Type StripNullable(this Type type)
    {
        // Similar official method: Nullable.GetUnderlyingType()
        return !IsNullableValueType(type) ? type : type.GenericTypeArguments[0];
    }

    /// <summary>
    /// Extracts the underlying element type from collection types, arrays, and nullable types.
    /// This method handles IEnumerable&lt;T&gt;, arrays, Nullable&lt;T&gt;, and other generic collection patterns
    /// to reveal the fundamental data type being stored or processed.
    /// </summary>
    /// <param name="type">The type to extract the underlying element type from</param>
    /// <returns>The underlying element type, or the original type if no underlying type can be determined</returns>
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
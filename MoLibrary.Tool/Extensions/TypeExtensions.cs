using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension method of <seealso cref="Type"/> type.
    /// </summary>
    public static class TypeExtensions
    {
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
        public static bool IsNullableValueType(this Type type) => type.GetTypeInfo().IsGenericType &&
                                                                  type.GetGenericTypeDefinition() == typeof(Nullable<>);
        /// <summary>
        /// Judge whether the specific type is derived from giving generic type or not.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="genericType">typeof(GenericType&lt;&gt;)</param>
        /// <returns></returns>
        public static bool IsDerivedFromGenericType(this Type type, Type genericType) =>
            type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == genericType;
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
}
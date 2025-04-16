using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// CustomAttribute扩展方法
    /// </summary>
    public static class CustomAttributeExtensions
    {
        #region CachedCustomAttribute 缓存版的CustomAttribute

        /// <summary>
        /// Cache Data [TypeName + AttributeName组成的Key, Attribute对象]
        /// </summary>
        private static readonly ConcurrentDictionary<string, object?> _cache = new();

        /// <summary>
        /// 获取指定类型的CustomAttribute
        /// </summary>
        /// <typeparam name="TAttribute">要获取的Attribute</typeparam>
        /// <returns>返回Attribute的值，没有则返回null</returns>
        public static TAttribute? GetCustomAttributeCached<TAttribute>(this Type classType)
            where TAttribute : Attribute
        {
            return GetCustomAttributeCached<TAttribute>(classType, null);
        }

        /// <summary>
        /// 获取指定类或指定属性或方法的CustomAttribute
        /// </summary>
        /// <typeparam name="TAttribute">要获取的Attribute</typeparam>
        /// <typeparam name="TClass"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <returns>返回Attribute的值，没有则返回null</returns>
        public static TAttribute? GetCustomAttributeCached<TAttribute, TClass, TProperty>(this Type classType,
            Expression<Func<TClass, TProperty>> property)
            where TAttribute : Attribute
        {
            var name = ((MemberExpression)property.Body).Member.Name;
            return GetCustomAttributeCached<TAttribute>(classType, name);
        }

        /// <summary>
        /// 获取指定类或指定属性或方法的CustomAttribute
        /// </summary>
        /// <typeparam name="TClass"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <typeparam name="TAttribute"></typeparam>
        /// <returns>返回Attribute的值，没有则返回null</returns>
        public static TAttribute? GetCustomAttributeCached<TAttribute, TClass, TProperty>(this TClass classType,
            TAttribute attributeType, Expression<Func<TClass, TProperty>> property) where TAttribute : Attribute where TClass : class, new()
        {
            var name = ((MemberExpression)property.Body).Member.Name;
            var type = typeof(TClass);
            return GetCustomAttributeCached<TAttribute>(type, name);
        }

        /// <summary>
        /// 获取指定类的指定属性或方法的CustomAttribute
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="sourceType">指定的类</param>
        /// <param name="name">指定属性或方法名</param>
        /// <returns>返回Attribute的值，没有则返回null</returns>
        public static TAttribute? GetCustomAttributeCached<TAttribute>(this Type sourceType, string name)
            where TAttribute : Attribute
        {
            var cacheKey = sourceType.FullName + "." + name + "." + typeof(TAttribute).FullName;
            var value = _cache.GetOrAdd(cacheKey, key => GetValue<TAttribute>(sourceType, name));
            if (value is TAttribute attribute) return attribute;
            return default;
        }
        /// <summary>
        /// 获取指定类或其属性或方法的CustomAttribute
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="type"></param>
        /// <param name="name">nameof</param>
        /// <returns>返回Attribute的值，没有则返回null</returns>
        private static TAttribute? GetValue<TAttribute>(Type type, string name)
            where TAttribute : Attribute
        {
            if (string.IsNullOrEmpty(name))
            {
                return type.GetCustomAttribute<TAttribute>(false);
            }

            var methodInfo = type.GetMethod(name);
            if (methodInfo != null)
            {
                return methodInfo.GetCustomAttribute<TAttribute>(false);
            }
            var propertyInfo = type.GetProperty(name);
            if (propertyInfo != null)
            {
                return propertyInfo.GetCustomAttribute<TAttribute>(false);
            }

            var fieldInfo = type.GetField(name);
            if (fieldInfo != null)
            {
                return fieldInfo.GetCustomAttribute<TAttribute>(false);
            }

            return default;
        }

        #endregion

    }
}

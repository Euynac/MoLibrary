using MoLibrary.Tool.Utils;
using System;
using System.Linq;
using System.Reflection;

namespace MoLibrary.Tool.Extensions;

/// <summary>
/// 反射相关
/// </summary>
public static partial class ObjectExtensions
{
    /// <summary>
    /// 克隆某个对象中所有可写的属性值到对象（引用类型依然是同个引用，值类型则是复制）
    /// </summary>
    /// <typeparam name="TTarget"></typeparam>
    /// <param name="fromObj"></param>
    /// <param name="ignoreParameterNames">设定忽略克隆的属性名</param>
    /// <returns>Return given cloned object for convenient.</returns>
    public static TTarget CloneAs<TTarget>(
        this object fromObj,
        params string[] ignoreParameterNames) where TTarget : class, new()
    {
        var hashSet = ignoreParameterNames.ToHashSet();
        var clonedObj = Activator.CreateInstance<TTarget>();
        var dict = fromObj.GetType().GetProperties().Where(p => p.CanRead).ToDictionary(p => p.Name, p => p);
        foreach (var targetInfo in typeof(TTarget).GetProperties().Where(p => p.CanWrite))
        {
            var name = targetInfo.Name;
            if (!dict.TryGetValue(name, out var fromInfo)) continue;
            var value = fromInfo.GetValue(fromObj);
            if (!hashSet.Contains(name))
                targetInfo.SetValue(clonedObj, value);
        }
        return clonedObj;
    }

    #region From ShardingCore

    private static readonly BindingFlags _bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>获取某字段值</summary>
    /// <param name="type">类型</param>
    /// <param name="obj">对象</param>
    /// <param name="fieldName">字段名</param>
    /// <returns></returns>
    public static object? GetTypeFieldValue(this Type type, object obj, string fieldName)
    {
        return type.GetField(fieldName, _bindingFlags)?.GetValue(obj);
    }

    /// <summary>获取某字段值</summary>
    /// <param name="obj">对象</param>
    /// <param name="fieldName">字段名</param>
    /// <returns></returns>
    public static object? GetFieldValue(this object obj, string fieldName)
    {
        return obj.GetType().GetField(fieldName, _bindingFlags)?.GetValue(obj);
    }

    /// <summary>获取某属性值</summary>
    /// <param name="obj">对象</param>
    /// <param name="propertyName">属性名</param>
    /// <returns></returns>
    public static object? GetPropertyValue(this object obj, string propertyName)
    {
        var shadowingProperty = obj.GetType().GetUltimateShadowingProperty(propertyName, _bindingFlags);
        return shadowingProperty != null ? shadowingProperty.GetValue(obj) : null;
    }

    /// <summary>获取某字段值</summary>
    /// <param name="type">类型</param>
    /// <param name="obj">对象</param>
    /// <param name="propertyName">属性名</param>
    /// <returns></returns>
    public static object? GetTypePropertyValue(this Type type, object obj, string propertyName)
    {
        var shadowingProperty = type.GetUltimateShadowingProperty(propertyName, _bindingFlags);
        return shadowingProperty != null ? shadowingProperty.GetValue(obj) : null;
    }

    public static PropertyInfo? GetObjectProperty(this object obj, string propertyName)
    {
        return obj.GetType().GetUltimateShadowingProperty(propertyName, _bindingFlags);
    }

    /// <summary>类型X是否包含某个属性</summary>
    /// <param name="type"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static bool ContainPropertyName(this Type type, string propertyName)
    {
        return type.GetUltimateShadowingProperty(propertyName, _bindingFlags) != null;
    }

    public static Type GetGenericType0(this Type genericType, Type arg0Type)
    {
        return genericType.MakeGenericType(arg0Type);
    }

    public static Type GetGenericType1(this Type genericType, Type arg0Type, Type arg1Type)
    {
        return genericType.MakeGenericType(arg0Type, arg1Type);
    }

    public static PropertyInfo? GetUltimateShadowingProperty(this Type type, string name)
    {
        return type.GetUltimateShadowingProperty(name, _bindingFlags);
    }

    /// <summary>
    /// https://github.com/nunit/nunit/blob/111fc6b5550f33b4fceb6ac8693c5692e99a5747/src/NUnitFramework/framework/Internal/Reflect.cs
    /// </summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <param name="bindingFlags"></param>
    /// <returns></returns>
    public static PropertyInfo? GetUltimateShadowingProperty(
      this Type type,
      string name,
      BindingFlags bindingFlags)
    {
        Check.NotNull<Type>(type, nameof(type));
        Check.NotNull<string>(name, nameof(name));
        if ((bindingFlags & BindingFlags.DeclaredOnly) != BindingFlags.Default)
            return type.GetProperty(name, bindingFlags);
        if ((bindingFlags & (BindingFlags.Public | BindingFlags.NonPublic)) == (BindingFlags.Public | BindingFlags.NonPublic))
        {
            for (var type1 = type; type1 != null; type1 = type1.GetTypeInfo().BaseType)
            {
                var property = type1.GetProperty(name, (bindingFlags | BindingFlags.DeclaredOnly) & ~BindingFlags.NonPublic);
                if (property != null)
                    return property;
            }
            bindingFlags &= ~BindingFlags.Public;
        }
        for (var type2 = type; type2 != null; type2 = type2.GetTypeInfo().BaseType)
        {
            var property = type2.GetProperty(name, bindingFlags | BindingFlags.DeclaredOnly);
            if (property != null)
                return property;
        }
        return null;
    }

    #endregion
}
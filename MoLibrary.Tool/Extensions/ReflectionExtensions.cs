using System;
using System.Linq;

namespace MoLibrary.Tool.Extensions;

public static class ReflectionExtensions
{
    /// <summary>
    /// 克隆某个对象中所有可写的属性值到对象（引用类型依然是同个引用，值类型则是复制）
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
}
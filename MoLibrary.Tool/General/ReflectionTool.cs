using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// 反射工具类
    /// </summary>
    public static class ReflectionTool
    {
        /// <summary>
        /// Extension of memberwise clone. Not recommend for production (Best practice is to each class needs clone define relative method). use for debug.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T ShallowCopy<T>(this T obj) where T : class
        {
            var method = obj.GetType().GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (T) method.Invoke(obj, null)!;
        }

        /// <summary>
        /// 克隆某个对象中所有可写的属性值到对象（引用类型依然是同个引用，值类型则是复制）（EFCore会追踪修改，因为做的是Action操作）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="copyFromObj">需要复制值的对象用以克隆</param>
        /// <param name="ignoreParameterNames">设定忽略克隆的属性名</param>
        /// <returns>Return given cloned object for convenient.</returns>
        public static T CloneParameters<T>(this T obj, T copyFromObj, params string[] ignoreParameterNames)
        {
            var ignoreList = ignoreParameterNames.ToHashSet();
            foreach (var propertyInfo in typeof(T).GetProperties().Where(p => p.CanWrite))
            {
                //获取属性值：
                var propertyValue = propertyInfo.GetValue(copyFromObj);
                //获取属性名：
                var propertyName = propertyInfo.Name;
                if (ignoreList.Contains(propertyName)) continue;
                propertyInfo.SetValue(obj, propertyValue);
            }
            return obj;
        }

        /// <summary>
        /// Use reflection to get all public property values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static IEnumerable<object?> GetPublicPropertyValues<T>(T instance) where T : class
        {
            var type = typeof(T);
            foreach (var propertyInfo in type.GetProperties())
            {
                //获取属性值：
                yield return propertyInfo.GetValue(instance);
            }
        }


        /// <summary>
        /// 获取指定类对象中所有public的属性信息，返回Dict[key属性名，Value[属性类型，属性值]]
        /// </summary>
        /// <typeparam name="T">指定类型</typeparam>
        /// <param name="instance">类的实例</param>
        /// <param name="toLowerCase">返回的属性名是否转小写</param>
        /// <returns></returns>
        public static Dictionary<string, KeyValuePair<Type, object>> GetAllPropertyInfo<T>(T instance, bool toLowerCase = false) where T : class
        {
            var propertyInfoDict = new Dictionary<string, KeyValuePair<Type, object>>();
            var type = typeof(T);
            foreach (var propertyInfo in type.GetProperties())
            {
                //获取属性类型
                var propertyType = propertyInfo.PropertyType;
                //获取属性名：
                var propertyName = toLowerCase ? propertyInfo.Name.ToLower() : propertyInfo.Name;
                //获取属性值：
                var propertyValue = propertyInfo.GetValue(instance);
                var propertyPair = new KeyValuePair<Type, object>(propertyType, propertyValue);
                propertyInfoDict.Add(propertyName, propertyPair);
            }
            return propertyInfoDict;
        }
    }
}

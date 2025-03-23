using System;
using System.Collections.Generic;
using System.Linq;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of attribute related type.
    /// </summary>
    public static class AttributeExtensions
    {
        #region Attribute类拓展
        /// <summary>
        /// 获取该类型上指定类型的CustomAttribute，包括其接口上的（仅支持类，不支持方法或属性等）。
        /// <br/>English:  Get the CustomAttribute of the specified type on the type, including those on its interface (only class is supported, not method or property, etc.).
        /// </summary>
        /// <typeparam name="T">指定的Attribute类型</typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IEnumerable<T> GetCustomAttributesIncludingBaseInterfaces<T>(this Type type)
        {
            var attributeType = typeof(T);
            return type.GetCustomAttributes(attributeType, true).
                Union(type.GetInterfaces().
                    SelectMany(interfaceType => interfaceType.GetCustomAttributes(attributeType, true))).
                Distinct().Cast<T>();
        }
        #endregion
    }
}
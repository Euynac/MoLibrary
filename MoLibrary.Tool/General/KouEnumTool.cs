using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MoLibrary.Tool.Extensions;

namespace Koubot.Tool.String
{
    /// <summary>
    /// 指定某枚举是KouEnum的Name，可以设定该Enum的转换名
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class KouEnumName : Attribute
    {
        /// <summary>
        /// 使用此类字符串来转换成该Enum
        /// </summary>
        public string[] Names { get; }
        /// <summary>
        /// 指示该枚举拥有的名字
        /// </summary>
        /// <param name="names"></param>
        public KouEnumName(params string[] names)
        {
            Names = names;
        }
    }
    /// <summary>
    /// KouEnum工具，可将string转换为对应的Enum
    /// </summary>
    public static class KouEnumTool
    {
        private static readonly ConcurrentDictionary<Type, Dictionary<string, Enum>>
            _enumCache = new();
        /// <summary>
        /// 通过字符串获取对应KouEnum标签枚举
        /// </summary>
        /// <param name="str"></param>
        /// <returns>失败抛出异常</returns>
        public static T ToKouEnum<T>(this string str) where T : struct, Enum
        {
            if (!TryToKouEnum(typeof(T), str, out var resultEnum))
            {
                throw new Exception($"{str} convert to {typeof(T).FullName} through KouEnumName failed");
            }
            return (T)resultEnum;
        }
        /// <summary>
        /// 尝试通过字符串获取对应KouEnum标签枚举
        /// </summary>
        /// <param name="str"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryToKouEnum<T>(this string? str, out T result) where T : struct, Enum
        {
            result = default;
            if (!TryToKouEnum(typeof(T), str??"", out var resultEnum)) return false;
            result = (T)resultEnum;
            return true;
        }

        /// <summary>
        /// 尝试通过字符串模糊获取对应KouEnum标签枚举
        /// </summary>
        /// <param name="str"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryToKouEnumFuzzy<T>(this string? str, out List<T> result) where T : struct, Enum
        {
            result = new List<T>();
            if (!TryToKouEnum(typeof(T), str??"", out var resultEnum, true)) return false;
            result = ((IEnumerable)resultEnum).Cast<T>().ToList();
            return true;
        }
        /// <summary>
        /// 读取 <see cref="System.Enum"/> 标记 <see cref="KouEnumName"/> 的标签对象
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static KouEnumName? GetKouEnumNameAttribute(this Enum? value) =>
            value?.GetType().GetCustomAttributeCached<KouEnumName>(value.ToString());

        /// <summary>
        /// 读取 <see cref="System.Enum"/> 标记 <see cref="KouEnumName"/> 第<paramref name="valueAt"/>个的值
        /// </summary>
        /// <param name="value">原始 <see cref="System.Enum"/> 值</param>
        /// <param name="valueAt"> <see cref="KouEnumName"/> 中的第几个值</param>
        /// <returns>如果成功获取返回特性标记的值，否则返回null</returns>
        public static string? GetKouEnumName(this Enum? value, int valueAt = 1)
            => value?.GetType().GetCustomAttributeCached<KouEnumName>(value.ToString())?.Names[valueAt - 1];
        /// <summary>
        /// 读取 <see cref="System.Enum"/> 标记 <see cref="KouEnumName"/> 的值或自身ToString
        /// </summary>
        /// <param name="value">原始 <see cref="System.Enum"/> 值</param>
        /// <param name="valueAt"> <see cref="KouEnumName"/> 中的第几个值</param>
        /// <returns>如果成功获取返回特性标记的值，否则返回自身自身ToString</returns>
        public static string? GetKouEnumNameOrString(this Enum? value, int valueAt = 1) => GetKouEnumName(value, valueAt) ?? value?.ToString();

        private static Dictionary<string, Enum> GetDict(Type type)
        {
            if (!_enumCache.TryGetValue(type, out Dictionary<string, Enum> enumDict))
            {
                CreateCache(type);//如果无缓存，自动创建该Enum缓存
                _enumCache.TryGetValue(type, out enumDict);
            }

            return enumDict!;
        }

        /// <summary>
        /// 尝试通过字符串获取对应KouEnum标签枚举
        /// </summary>
        /// <param name="enumType">枚举的类型</param>
        /// <param name="str"></param>
        /// <param name="result"></param>
        /// <param name="fuzzy">模糊则返回的是List</param>
        /// <returns></returns>
        public static bool TryToKouEnum(Type enumType, string str, out object result, bool fuzzy = false)
        {
            result = default!;
            var enumDict = GetDict(enumType);
            switch (fuzzy)
            {
                case false when enumDict.TryGetValue(str, out var enumResult):
                    result = enumResult;
                    return true;
                case true:
                {
                    var list = enumDict.Where(p => p.Key.Contains(str, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).ToList();
                    result = list;
                    return list.Count > 0;
                }
            }

            return false;
        }
        /// <summary>
        /// 创建该enum的枚举name缓存
        /// </summary>
        /// <param name="enumType"></param>
        private static void CreateCache(Type enumType)
        {
            var enumValues = Enum.GetValues(enumType);
            var enumNames = Enum.GetNames(enumType);
            Dictionary<string, Enum> enumsNameDict = new Dictionary<string, Enum>();
            var i = 0;
            foreach (var value in enumValues)
            {
                enumsNameDict.Add(enumNames[i], (Enum)value);
                var nameEnum = enumType.GetField(enumNames[i])?.GetCustomAttribute<KouEnumName>(false);
                i++;
                if (nameEnum == null) continue;
                foreach (var name in nameEnum.Names)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    enumsNameDict[name] = (Enum)value;
                }
                
            }
            _enumCache.TryAdd(enumType, enumsNameDict);
        }
    }
}
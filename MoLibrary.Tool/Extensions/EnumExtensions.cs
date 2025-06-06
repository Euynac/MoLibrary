using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension method of Enum related type
    /// </summary>
    public static class EnumExtensions
    {
        #region Enum类拓展
        /// <summary>
        /// Converts the string representation of the name or numeric value of one or more enumerated constants to an equivalent enumerated object.
        /// A parameter specifies whether the operation is case-sensitive.
        /// The return value indicates whether the conversion succeeded.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type to which to convert <paramref name="value" />.</typeparam>
        /// <param name="value">The string representation of the enumeration name or underlying value to convert.</param>
        /// <param name="ignoreCase">
        /// <see langword="true" /> to ignore case; <see langword="false" /> to consider case.</param>
        /// <returns><typeparamref name="TEnum"/> if the <paramref name="value" /> parameter was converted successfully; otherwise, <see langword="null" />.</returns>
        public static TEnum? ToEnum<TEnum>(this string value, bool ignoreCase = true) where TEnum : struct, Enum
        {
            if (Enum.TryParse(value, ignoreCase, out TEnum result)) return result;
            return null;
        }

        /// <summary>
        /// Get all alternative values of specific Enum class.
        /// <para>Actually is the method of Enum.GetValues().</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="anyEnumValue"></param>
        /// <returns></returns>
        public static T[] GetAllValues<T>(this T anyEnumValue) where T : struct, Enum
            => Enum.GetValues(typeof(T)).Cast<T>().ToArray();


        /// <summary>
        /// 读取 <see cref="System.Enum"/> 标记 <see cref="System.ComponentModel.DescriptionAttribute"/> 的值。
        /// <br/>English: Get the value of <see cref="System.ComponentModel.DescriptionAttribute"/> of <see cref="System.Enum"/>.
        /// </summary>
        /// <param name="value">原始 <see cref="System.Enum"/> 值</param>
        /// <param name="notReturnDefaultEnum">找不到标签值时不返回给定的枚举的<seealso cref="string"/>形式，直接返回null</param>
        /// <returns>如果成功获取返回特性标记的值，否则返回给定的枚举的<seealso cref="string"/>形式，或 null。
        ///<br/>English: If the value is successfully obtained, the value of the attribute tag is returned, otherwise the <seealso cref="string"/> form of the given enumeration is returned, or null.
        /// </returns>
        [ContractAnnotation("notReturnDefaultEnum:false => notnull")]
        public static string? GetDescription(this Enum value, bool notReturnDefaultEnum = false)
        {
            var attribute = value.GetType().GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>(false);
            return attribute?.Description ?? (notReturnDefaultEnum ? null : value.ToString());
        }

        /// <summary>
        /// Convert the enum value to corresponding int.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int ToInt(this Enum value) => Convert.ToInt32(value);

        /// <summary>
        /// 使用指定分割符批量格式化按位枚举<see cref="System.Enum"/> 中含有的枚举值。格式化方式是使用 <see cref="System.ComponentModel.DescriptionAttribute"/>标记的值或string类型枚举。
        /// <br/>English: Format the enumeration values contained in the bit enumeration <see cref="System.Enum"/> in batches using the specified separator. The formatting method is the value marked by <see cref="System.ComponentModel.DescriptionAttribute"/> or string type enumeration.
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="ignoreNoDesc">忽略没有Description特性的字段</param>
        /// <param name="ignoreEnums">忽略格式化的Enum值</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static string GetFlagsDescription<T>(this T flags, char separator = '、', bool ignoreNoDesc = false, params T[] ignoreEnums) where T : Enum
        {
            var enumValues = Enum.GetValues(typeof(T));
            var stringBuilder = new StringBuilder();
            var ignoreList = ignoreEnums.ToHashSet();
            foreach (var value in enumValues)
            {
                var v = (T)value;
                if (ignoreList.Contains(v)) continue;
                if (flags.HasFlag(v))
                {
                    var tmp = v.GetDescription(ignoreNoDesc);
                    if (tmp == null) continue;
                    stringBuilder.Append(tmp);
                    stringBuilder.Append(separator);
                }
            }

            return stringBuilder.ToString().TrimEnd(separator);
        }

        /// <summary>
        /// 将字符串类型的Flags值变为其对应的枚举对象（一般用于GetFlagsString方法转换回去）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="flagsStr"></param>
        /// <param name="separator"></param>
        /// <param name="ignoreCase"></param>
        /// <param name="strictMode">如果无法处理为相应Flag就报错</param>
        /// <returns></returns>
        public static T RetrieveFlags<T>(this string flagsStr, char separator, bool ignoreCase = false, bool strictMode = true) where T: struct, Enum
        {
            var e = 0;
            foreach (var flag in flagsStr.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Enum.TryParse(typeof(T), flag, ignoreCase, out var tmpFlag) && strictMode)
                {
                    throw new IndexOutOfRangeException($"{flagsStr} can't not parse to {typeof(T).Name}");
                }

                e |= tmpFlag?.GetHashCode() ?? 0;
            }
            return (T)(e as object);
        }
        /// <summary>
        /// 一个一个返回给定按位枚举中含有的枚举值。
        /// <br/>English: Returns the enumeration values contained in the bit enumeration given one by one.
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="ignoreEnums">忽略格式化的Enum值</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> GetFlags<T>(this T flags, params T[] ignoreEnums) where T : Enum
        {
            var enumValues = Enum.GetValues(typeof(T));
            var ignoreList = ignoreEnums.ToHashSet();
            foreach (var value in enumValues)
            {
                var v = (T)value;
                if (ignoreList.Contains(v)) continue;
                if (flags.HasFlag(v))
                {
                    yield return (T)value;
                }
            }
        }
        /// <summary>
        /// 使用指定分割符批量格式化给定按位枚举中含有的枚举值。格式化方式是使用string类型枚举。
        /// <br/>English: Format the enumeration values contained in the given bit enumeration in batches using the specified separator. The formatting method is the string type enumeration.
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="ignoreEnums">忽略格式化的Enum值</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static string GetFlagsString<T>(this T flags, char separator = ',', params T[] ignoreEnums) where T : Enum
        {
            var enumValues = Enum.GetValues(typeof(T));
            var stringBuilder = new StringBuilder();
            var ignoreList = ignoreEnums.ToHashSet();
            foreach (var value in enumValues)
            {
                var v = (T)value;
                if (ignoreList.Contains(v)) continue;
                if (flags.HasFlag(v))
                {
                    stringBuilder.Append(v);
                    stringBuilder.Append(separator);
                }
            }

            return stringBuilder.ToString().TrimEnd(separator);
        }
        /// <summary>
        /// 移除按位枚举中的指定枚举。
        /// <br/>English: Remove the specified enumeration from the bit enumeration.
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="removeFlags">要移除的枚举</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Remove<T>(this T flags, params T[] removeFlags) where T : Enum
        {
            var flagInt = flags.GetHashCode();
            foreach (var removeFlag in removeFlags)//其实var直接用dynamic更好，不过当前版本不支持?
            {
                flagInt &= ~removeFlag.GetHashCode();//避免装箱产生过多损耗 
            }

            return (T)(flagInt as object);
        }
        /// <summary>
        /// 添加指定枚举到指定按位枚举中。
        /// <br/>English: Add the specified enumeration to the specified bit enumeration.
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="addFlags">要添加的枚举</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Add<T>(this T flags, params T[] addFlags) where T : Enum
        {
            var flagInt = flags.GetHashCode();
            foreach (var flag in addFlags)
            {
                flagInt |= flag.GetHashCode();
            }

            return (T)(flagInt as object);
        }

        /// <summary>
        /// 从指定按位枚举中，添加或删除给定枚举。
        /// <br/>English: Add or remove the given enumeration from the specified bit enumeration.
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="judgeAdd">如果为true，说明要添加，否则要删除</param>
        /// <param name="alterFlags">要添加或删除的枚举</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T AddOrRemove<T>(this T flags, bool judgeAdd, params T[] alterFlags) where T : Enum
        {
            return judgeAdd ? Add(flags, alterFlags) : Remove(flags, alterFlags);
        }

        /// <summary>
        /// 判断按位枚举是否存在指定的任意一个选项。
        /// <br/>English: Determine if the bit enumeration has any of the specified options.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static bool HasAnyFlag<T>(this T value, params T[] flags) where T : Enum
        {
            return flags.Any(flag => value.HasFlag(flag));
        }
        /// <summary>
        /// 判断按位枚举是否存在指定的所有选项。
        /// <br/>English: Determine if the bit enumeration has all of the specified options.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static bool HasAllFlag<T>(this T value, params T[] flags) where T : Enum
        {
            return flags.All(flag => value.HasFlag(flag));
        }
        /// <summary>
        /// 判断按位枚举是否存在指定的选项。
        /// <br/>English: Determine if the bit enumeration has the specified option.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool HasTheFlag<T>(this T value, T flag) where T : Enum
            => value.HasFlag(flag);

        #endregion
    }
}
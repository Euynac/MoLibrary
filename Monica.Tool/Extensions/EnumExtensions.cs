using System.ComponentModel;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;

namespace Monica.Tool.Extensions;

/// <summary>
/// Extension method of Enum related type
/// </summary>
public static class EnumExtensions
{
    #region Enum Extensions
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
    /// Reads the value of <see cref="System.ComponentModel.DescriptionAttribute"/> from a marked <see cref="System.Enum"/>.
    /// </summary>
    /// <param name="value">original <see cref="System.Enum"/> value</param>
    /// <param name="notReturnDefaultEnum">Does not return the given enum when the tag value is not found<seealso cref="string"/>form, directly returns null</param>
    /// <returns>
    /// Returns the attribute value when available; otherwise returns the enumeration value in <seealso cref="string"/> form, or null.
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
    /// Batch-formats values contained in a flags <see cref="System.Enum"/> using the specified separator.
    /// Values are formatted by <see cref="System.ComponentModel.DescriptionAttribute"/> when available, otherwise by enum string value.
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="separator">delimiter</param>
    /// <param name="ignoreNoDesc">Ignore fields without Description attribute</param>
    /// <param name="ignoreEnums">Ignore formatted Enum values</param>
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
    /// Convert the Flags value of the string type into its corresponding enumeration object (generally used to convert it back using the GetFlagsString method)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="flagsStr"></param>
    /// <param name="separator"></param>
    /// <param name="ignoreCase"></param>
    /// <param name="strictMode">If it cannot be processed into the corresponding Flag, an error will be reported.</param>
    /// <returns></returns>
    public static T RetrieveFlags<T>(this string flagsStr, char separator, bool ignoreCase = false, bool strictMode = true) where T: struct, Enum
    {
        ArgumentNullException.ThrowIfNull(flagsStr);

        ulong combined = 0;
        foreach (var flag in flagsStr.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<T>(flag, ignoreCase, out var parsedFlag))
            {
                if (strictMode)
                {
                    throw new IndexOutOfRangeException($"{flagsStr} cannot be parsed as {typeof(T).Name}");
                }

                continue;
            }

            combined |= ToUInt64(parsedFlag);
        }

        return FromUInt64<T>(combined);
    }
    /// <summary>
    /// Returns the enumeration values ​​contained in the given bitwise enumeration one by one.
    /// <br/>English: Returns the enumeration values contained in the bit enumeration given one by one.
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="ignoreEnums">Ignore formatted Enum values</param>
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
    /// Batch formats the enumeration values ​​contained in the given bitwise enumeration using the specified delimiter. The formatting method is to use string type enumeration.
    /// <br/>English: Format the enumeration values contained in the given bit enumeration in batches using the specified separator. The formatting method is the string type enumeration.
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="separator">delimiter</param>
    /// <param name="ignoreEnums">Ignore formatted Enum values</param>
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
    /// Removes the specified enumeration from the bitwise enumeration.
    /// <br/>English: Remove the specified enumeration from the bit enumeration.
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="removeFlags">enum to remove</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T Remove<T>(this T flags, params T[] removeFlags) where T : Enum
    {
        var value = ToUInt64(flags);
        foreach (var removeFlag in removeFlags)
        {
            value &= ~ToUInt64(removeFlag);
        }

        return FromUInt64<T>(value);
    }
    /// <summary>
    /// Adds the specified enumeration to the specified bitwise enumeration.
    /// <br/>English: Add the specified enumeration to the specified bit enumeration.
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="addFlags">enum to add</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T Add<T>(this T flags, params T[] addFlags) where T : Enum
    {
        var value = ToUInt64(flags);
        foreach (var flag in addFlags)
        {
            value |= ToUInt64(flag);
        }

        return FromUInt64<T>(value);
    }

    /// <summary>
    /// Adds or removes the given enumeration from the specified bitwise enumeration.
    /// <br/>English: Add or remove the given enumeration from the specified bit enumeration.
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="judgeAdd">If true, it means to add, otherwise it needs to be deleted.</param>
    /// <param name="alterFlags">enum to add or remove</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T AddOrRemove<T>(this T flags, bool judgeAdd, params T[] alterFlags) where T : Enum
    {
        return judgeAdd ? Add(flags, alterFlags) : Remove(flags, alterFlags);
    }

    /// <summary>
    /// Determine whether any of the specified options exists in the bitwise enumeration.
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
    /// Determines whether all specified options exist in a bitwise enumeration.
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
    /// Determines whether the specified option exists in a bitwise enumeration.
    /// <br/>English: Determine if the bit enumeration has the specified option.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="flag"></param>
    /// <returns></returns>
    public static bool HasTheFlag<T>(this T value, T flag) where T : Enum
        => value.HasFlag(flag);

    private static ulong ToUInt64<T>(T value) where T : Enum
    {
        return Type.GetTypeCode(Enum.GetUnderlyingType(typeof(T))) switch
        {
            TypeCode.SByte => unchecked((ulong)Convert.ToSByte(value)),
            TypeCode.Int16 => unchecked((ulong)Convert.ToInt16(value)),
            TypeCode.Int32 => unchecked((ulong)Convert.ToInt32(value)),
            TypeCode.Int64 => unchecked((ulong)Convert.ToInt64(value)),
            TypeCode.Byte => Convert.ToByte(value),
            TypeCode.UInt16 => Convert.ToUInt16(value),
            TypeCode.UInt32 => Convert.ToUInt32(value),
            TypeCode.UInt64 => Convert.ToUInt64(value),
            _ => throw new InvalidOperationException($"Unsupported enum underlying type for {typeof(T).FullName}.")
        };
    }

    private static T FromUInt64<T>(ulong value) where T : Enum
    {
        object underlyingValue = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(T))) switch
        {
            TypeCode.SByte => unchecked((sbyte)value),
            TypeCode.Int16 => unchecked((short)value),
            TypeCode.Int32 => unchecked((int)value),
            TypeCode.Int64 => unchecked((long)value),
            TypeCode.Byte => unchecked((byte)value),
            TypeCode.UInt16 => unchecked((ushort)value),
            TypeCode.UInt32 => unchecked((uint)value),
            TypeCode.UInt64 => value,
            _ => throw new InvalidOperationException($"Unsupported enum underlying type for {typeof(T).FullName}.")
        };

        return (T)Enum.ToObject(typeof(T), underlyingValue);
    }

    #endregion
}

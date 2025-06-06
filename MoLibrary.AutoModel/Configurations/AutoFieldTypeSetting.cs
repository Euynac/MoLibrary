using System.Collections;
using System.Text.Json.Serialization;
using MoLibrary.AutoModel.Exceptions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.AutoModel.Configurations;

public class AutoFieldTypeSetting
{
    /// <summary>
    /// 来源定义类型
    /// </summary>
    [JsonIgnore] public Type? DeclaringType { get; set; }
    /// <summary>
    /// 原始参数的类型
    /// </summary>
    [JsonIgnore] public Type OriginType { get; set; } = null!;

    /// <summary>
    /// 原始参数的类型的潜在类型(即去除nullable、IEnumerable之后的纯粹类型)
    /// </summary>
    [JsonIgnore] public Type OriginUnderlyingType { get; set; } = null!;

    /// <summary>
    /// 参数类型特征
    /// </summary>
    public ETypeFeatures TypeFeatures { get; set; }

    /// <summary>
    /// 基本类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EBasicType BasicType { get; set; }

    /// <summary>
    /// 原始参数类型名
    /// </summary>
    public string TypeName => OriginUnderlyingType.Name;

    public AutoFieldTypeSetting(Type parameterType, Type? declaringType)
    {
        DeclaringType = declaringType;
        AutoSetTypeSetting(parameterType);
    }

    /// <summary>
    /// 根据参数类型自动设置TypeSetting(比如OriginType、TypeClass、OriginUnderlyingType等)
    /// </summary>
    /// <param name="parameterType"></param>
    private void AutoSetTypeSetting(Type parameterType)
    {
        OriginType = parameterType;
        BasicType = GetTypeClass(parameterType, out var underlyingType, out var features);
        TypeFeatures = features;
        OriginUnderlyingType = underlyingType;
    }

    /// <summary>
    /// 获取指定类型的BasicType
    /// </summary>
    /// <param name="parameterType"></param>
    /// <param name="underlyingType"></param>
    /// <param name="features"></param>
    /// <returns></returns>
    public static EBasicType GetTypeClass(Type? parameterType, out Type underlyingType, out ETypeFeatures features)
    {
        var origin = parameterType;
        if (origin == null) throw new Exception("传入的parameterType为null，无法判断SpecialKind");
        var originParameterTypeName = parameterType!.FullName;
        features = ETypeFeatures.None;
        if (typeof(IEnumerable).IsAssignableFrom(parameterType) && parameterType != typeof(string))
        {
            features |= ETypeFeatures.IsCollection;
            if (parameterType.IsGenericType)
            {
                parameterType = parameterType.GetGenericArguments().FirstOrDefault();
            }
            else if (parameterType.IsArray)
            {
                parameterType = parameterType.GetElementType();
            }

            if (parameterType == null)
                throw new AutoModelSnapshotNotSupportTypeException($"不支持该功能参数类型{originParameterTypeName}",
                    origin);
        }

        if (parameterType.IsDerivedFromGenericType(typeof(ThreadLocal<>)))
        {
            features |= ETypeFeatures.IsThreadLocal;
            parameterType = parameterType.GetGenericArguments().FirstOrDefault();
            if (parameterType == null)
                throw new AutoModelSnapshotNotSupportTypeException($"不支持该功能参数类型{originParameterTypeName}",
                    origin);
        }
        if (parameterType.IsNullableValueType())
        {
            features |= ETypeFeatures.IsNullable;
            parameterType = Nullable.GetUnderlyingType(parameterType)!;
        }
        if (parameterType is { IsClass: true, IsGenericType: true } && parameterType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            features |= ETypeFeatures.IsNullable;
            parameterType = Nullable.GetUnderlyingType(parameterType)!;
        }


        underlyingType = parameterType;
        if (underlyingType.IsEnum)
        {
            return EBasicType.IsEnum;
        }


        if (underlyingType == typeof(string)) return EBasicType.IsString;
        if (underlyingType == typeof(bool)) return EBasicType.IsBoolean;
        if (underlyingType == typeof(Guid)) return EBasicType.IsGuid;
        if (underlyingType == typeof(DateTime)) return EBasicType.IsDateTime;
        if (underlyingType == typeof(TimeSpan)) return EBasicType.IsTimeSpan;
        if (underlyingType == typeof(TimeOnly)) return EBasicType.IsTimeOnly;
        if (underlyingType == typeof(DateOnly)) return EBasicType.IsDateOnly;



        if (underlyingType == typeof(double))
        {
            features |= ETypeFeatures.IsNumeric;
            return EBasicType.IsDouble;
        }

        if (underlyingType == typeof(float))
        {
            features |= ETypeFeatures.IsNumeric;
            return EBasicType.IsFloat;
        }

        if (underlyingType == typeof(int))
        {
            features |= ETypeFeatures.IsNumeric;
            return EBasicType.IsInt;
        }

        if (underlyingType == typeof(long))
        {
            features |= ETypeFeatures.IsNumeric;
            return EBasicType.IsLong;
        }

        if (underlyingType == typeof(decimal))
        {
            features |= ETypeFeatures.IsNumeric;
            return EBasicType.IsDecimal;
        }


        if (underlyingType == typeof(char)) return EBasicType.IsChar;
        if (underlyingType.IsClass) return EBasicType.IsClass;//string is also class.
        throw new AutoModelSnapshotNotSupportTypeException($"暂不支持的类型{underlyingType.GetCleanFullName()}", origin);
    }

    public override string ToString()
    {
        return
            $"{BasicType}{TypeFeatures.GetFlagsString(ignoreEnums: ETypeFeatures.None).BeIfNotEmpty(" and {0}", true)}{DeclaringType?.GetCleanFullName().Be("(from {0})", true)}";
    }
}

[Flags]
public enum ETypeFeatures
{
    None,
    IsCollection = 1 << 0,
    IsNullable = 1 << 1,
    IsThreadLocal = 1 << 2,
    IsNumeric = 1 << 3,
    IsClass = 1 << 5,
}

public enum EBasicType
{
    IsInt,
    IsDouble,
    IsLong,
    IsEnum,
    IsString,
    IsBoolean,
    IsDateTime,
    IsGuid,
    IsTimeSpan,
    IsChar,
    IsClass,
    IsFloat,
    IsTimeOnly,
    IsDateOnly,
    IsDecimal,
}
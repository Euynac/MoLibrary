using Microsoft.Extensions.Options;
using MoLibrary.AutoModel.AutoModel.Implements;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using MoLibrary.Tool.Extensions;
using System.Collections;
using MoLibrary.AutoModel.Exceptions;
using MoLibrary.Tool.General;

namespace MoLibrary.AutoModel.Implements;

public class AutoModelTypeConverter(IOptions<AutoModelExpressionOptions> options) : IAutoModelTypeConverter
{
    protected AutoModelExpressionOptions Options = options.Value;

    //System.Dynamic.Linq不支持List<object>类型的Contains之类的泛型方法，必须强类型List
    private static IList ToStrongTypeList<T>(IEnumerable<T> enumerable)
    {
        var list = enumerable.ToList();
        if (list.FirstOrDefault()?.GetType() is { } type)
        {
            var listType = typeof(List<>).MakeGenericType(type);
            var typedList = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in list)
            {
                typedList.Add(item);
            }
            return typedList;
        }
        return list;
    }

    public dynamic? ConvertEntrance(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if (features.HasTheFlag(EFieldConditionFeatures.Multi) && value.Contains(Options.FilterMultiSeparator))
        {
            return ToStrongTypeList(value.Split(Options.FilterMultiSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(singleValue => ConvertEntrance(singleValue, typeSetting, features)));
        }

        switch (typeSetting.BasicType)
        {
            case EBasicType.IsDouble:
                return ConvertDouble(value, typeSetting, features);
            case EBasicType.IsInt:
                return ConvertInt(value, typeSetting, features);
            case EBasicType.IsLong:
                return ConvertLong(value, typeSetting, features);
            case EBasicType.IsDecimal:
                return ConvertDecimal(value, typeSetting, features);
            case EBasicType.IsEnum:
                return ConvertEnum(value, typeSetting, features);
            case EBasicType.IsString:
                return ConvertString(value, typeSetting, features);
            case EBasicType.IsBoolean:
                return ConvertBoolean(value, typeSetting, features);
            case EBasicType.IsDateTime:
                return ConvertDateTime(value, typeSetting, features);
            case EBasicType.IsDateOnly:
                return ConvertDateOnly(value, typeSetting, features);
            case EBasicType.IsTimeOnly:
                return ConvertTimeOnly(value, typeSetting, features);
            case EBasicType.IsGuid:
                return ConvertGuid(value, typeSetting, features);
            case EBasicType.IsTimeSpan:
                return ConvertTimeSpan(value, typeSetting, features);
            case EBasicType.IsChar:
            case EBasicType.IsClass:
            case EBasicType.IsFloat:
            default: throw new AutoModelValueConvertException($"不支持转换的参数类型{typeSetting.OriginType.FullName}");
        }
    }


    public dynamic ConvertBoolean(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if (KouStringTool.TryToBool(value.ToLower(), out var boolResult))
        {
            return boolResult;
        }

        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }

        throw new AutoModelValueConvertException($"无法转换{value}为Boolean");
    }

    #region DateTime相关
    public dynamic ConvertTimeSpan(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }
        if (TimeSpan.TryParse(value, out var timeSpanResult))
        {
            return timeSpanResult;
        }

        throw new AutoModelValueConvertException($"无法转换{value}为TimeSpan");
    }


    public dynamic ConvertDateTime(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }

        if (MoStringTool.TryToDateTime(value, out var dateTime))
        {
            return dateTime;
        }

        throw new AutoModelValueConvertException($"无法转换{value}为DateTime");
    }

    public dynamic ConvertTimeOnly(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
            //截至2024-04-29 PgSQL provider 暂不支持TimeOnly.ToString()
        }
        if (MoStringTool.TryToTimeOnly(value, out var time))
        {
            return time;
        }

        throw new AutoModelValueConvertException($"无法转换{value}为TimeOnly");
    }
    public dynamic ConvertDateOnly(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {

        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
            //截至2024-04-29 PgSQL provider 暂不支持DateOnly.ToString()
        }

        if (MoStringTool.TryToDateOnly(value, out var time))
        {
            return time;
        }

        throw new AutoModelValueConvertException($"无法转换{value}为DateOnly");
    }
    #endregion



    #region Numeric相关

    public dynamic ConvertDouble(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }

        if (double.TryParse(value, out var doubleResult))
        {
            return doubleResult;
        }

        throw new AutoModelValueConvertException($"无法转换{value}为数字double类型");
    }

    public dynamic ConvertLong(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }
        if (long.TryParse(value, out var result))
        {
            return result;
        }
        throw new AutoModelValueConvertException($"无法转换{value}为数字long类型");
    }

    public dynamic ConvertInt(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }
        if (int.TryParse(value, out var result))
        {
            return result;
        }
        throw new AutoModelValueConvertException($"无法转换{value}为数字int类型");
    }

    public dynamic ConvertDecimal(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }
        if (decimal.TryParse(value, out var result))
        {
            return result;
        }
        throw new AutoModelValueConvertException($"无法转换{value}为数字decimal类型");
    }
    #endregion


    public dynamic ConvertEnum(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            KouEnumTool.TryToKouEnum(typeSetting.OriginUnderlyingType, value, out var enumResultList, true);
            return enumResultList is IList { Count: 0 }
                ? FieldResult.JumpThisField()
                : ((List<Enum>)enumResultList)
                .Select(p => Convert.ChangeType(p, typeSetting.OriginUnderlyingType))
                .ToList().ConvertToSpecificItemType(typeSetting.OriginUnderlyingType);
        }
        try
        {
            return Enum.Parse(typeSetting.OriginUnderlyingType, value, true);
        }
        catch (Exception e)
        {
            throw new AutoModelValueConvertException(e.Message);
        }
    }
    public dynamic ConvertString(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value.Contains('%') ? value : $"%{value}%";
        }
        return value;
    }
    public dynamic ConvertGuid(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features)
    {
        if ((features & EFieldConditionFeatures.Fuzzy) != 0)
        {
            return value;
        }
        if (Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        throw new AutoModelValueConvertException($"无法转换{value}为Guid");
    }


}
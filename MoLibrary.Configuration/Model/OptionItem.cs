using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using MoLibrary.Configuration.Annotations;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Configuration.Model;

/// <summary>
/// 配置项信息类
/// </summary>
public class OptionItem
{
    /// <summary>
    /// 配置项信息类
    /// </summary>
    public OptionItem(OptionSettingAttribute? info, PropertyInfo property, object? configInstance, string? parentKey)
    {
        Info = info;
        PropertyInfo = property;
        NormalizeType(PropertyInfo.PropertyType);
        Key = string.IsNullOrEmpty(parentKey) ? property.Name : $"{parentKey}:{property.Name}";
        Value = configInstance is { } instance ? property.GetValue(instance) : null;
        ValidateRegexPattern = GenValidateRegexPattern(property);

        if (info is null && MoConfigurationManager.Setting.ErrorOnNoTagOptionAttribute)
        {
            throw new InvalidOperationException(
                $"李振主任要求：每个配置项必须要写中文名称，有备注必须写备注，请负责各个配置类的人完善;Property {property.Name} of Type {property.DeclaringType?.FullName} is not tagged with {typeof(OptionSettingAttribute)}.");
        }
    }

    /// <summary>
    /// 配置信息特性
    /// </summary>
    public OptionSettingAttribute? Info { get; }
    /// <summary>
    /// 配置项原始参数名
    /// </summary>
    public string Name => PropertyInfo.Name;
    /// <summary>
    /// 配置项显示名
    /// </summary>
    public string Title => Info?.Title ?? PropertyInfo.Name;
    /// <summary>
    /// 配置描述
    /// </summary>
    public string? Description => Info?.Title;
    /// <summary>
    /// 配置项属性反射信息
    /// </summary>
    public PropertyInfo PropertyInfo { get; }
    /// <summary>
    /// 配置基本类型
    /// </summary>
    public EOptionItemValueBasicType BasicType { get; set; }
    /// <summary>
    /// 配置基本的系统类型，去除nullable、List等泛型类型后的纯净类型
    /// </summary>
    public Type UnderlyingType { get; private set; }
    /// <summary>
    /// 配置特殊类型
    /// </summary>
    public EOptionItemValueSpecialType? SpecialType { get; set; }
    /// <summary>
    /// 使用:拼接作为Key，与原生Configuration Key保持一致
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 配置项值
    /// </summary>
    public object? Value { get; private set; }

    /// <summary>
    /// 当配置项值为Dictionary或List或类的配置类类型，将会有子配置的配置类信息
    /// </summary>
    public MoConfiguration? SubConfigInfo { get; set; }

    #region Validate

    /// <summary>
    /// 验证正则表达式
    /// </summary>
    public string? ValidateRegexPattern { get; set; }

    #endregion


    #region 来源信息

    /// <summary>
    /// 配置类绑定到的配置来源
    /// </summary>
    public string? Provider { get; internal set; }

    /// <summary>
    /// 最终配置来源详细
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 所有配置来源，越后优先级越高
    /// </summary>
    public List<string> SourceList { get; set; } = [];
    public void SetSource(IConfigurationProvider provider, string? sourceInfo)
    {
        Provider = provider.GetType().Name;
        Source = sourceInfo;
        if (sourceInfo != null)
        {
            SourceList.Add(sourceInfo);
        }
    }
    #endregion

    public void SetValueFromConfigInstance(object? instance)
    {
        Value = PropertyInfo.GetValue(instance);
        if (PropertyInfo.PropertyType is {IsGenericType: true} type)
        {
            if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GenericTypeArguments[1] is {IsClass:true} dictValueType && UtilsConfiguration.HasConfigAttribute(dictValueType))
            {
                SubConfigInfo = new MoConfiguration(dictValueType);//字典嵌套配置类
            }
            else if (type.GetGenericTypeDefinition() == typeof(List<>) &&
                     type.GenericTypeArguments[0] is {IsClass: true} listValueType &&
                     UtilsConfiguration.HasConfigAttribute(listValueType))
            {
                SubConfigInfo = new MoConfiguration(listValueType);//列表嵌套配置类
            }
        }
        else if(PropertyInfo.PropertyType is {IsClass:true, IsArray:false} propertyType && propertyType != typeof(string))
        {
            SubConfigInfo = new MoConfiguration(propertyType);//单配置类
        }
    }

    /// <summary>
    /// 根据配置类获取配置项信息列表
    /// </summary>
    /// <param name="configType"></param>
    /// <param name="configInstance"></param>
    /// <param name="parentKey">即配置节点SectionName</param>
    /// <returns></returns>
    public static List<OptionItem> CreateItems(Type configType, object? configInstance, string? parentKey)
    {
        var items = configType.GetProperties()
            .Select(x => new OptionItem(x.GetCustomAttribute<OptionSettingAttribute>(), x, configInstance, parentKey)).ToList();
        return items;
    }

    public override string ToString()
    {
        if (Value == null)
        {
            return $"{Key}：<null>";
        }

        if (Info?.LoggingFormat is { } format)
        {
            return string.Format(format, Value);
        }
        return $"{Key}: {Value}";
    }

    /// <summary>
    /// 根据类型及设置生成验证的正则表达式
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    private string? GenValidateRegexPattern(PropertyInfo property)
    {
        var setting = property.GetCustomAttribute<RegularExpressionAttribute>()?.Pattern;
        if (setting != null) return setting;
        if (BasicType == EOptionItemValueBasicType.Enum)
        {
            var values = Enum.GetNames(UnderlyingType);
            return $"^({values.StringJoin("|")})$";
        }


        return null;
    }

    #region 类型正规化

    private static readonly HashSet<Type> NumericSet = [typeof(int), typeof(long), typeof(double)];
    private static readonly HashSet<Type> DateTimeSet = [typeof(DateTime)];

    private void NormalizeType(Type type)
    {
        UnderlyingType = type;
        if (type.IsArray)
        {
            SpecialType = EOptionItemValueSpecialType.Array;
            UnderlyingType = type.GetElementType() ?? throw new Exception($"不被支持的数组类型：{type.FullName}，其元素类型无法获取");
        }
        else if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(List<>))
            {
                SpecialType = EOptionItemValueSpecialType.Array;
                UnderlyingType = type.GetGenericArguments().First();
            }

            if (generic == typeof(Dictionary<,>))
            {
                SpecialType = EOptionItemValueSpecialType.Dict;
                UnderlyingType = type.GetGenericArguments().First();
            }
        }

        if (UnderlyingType == null)
        {
            throw new Exception($"暂不支持的类型：{type.FullName} 请上报以支持");
        }


        if (UnderlyingType.IsValueType)
        {
            UnderlyingType = UnderlyingType.StripNullable();
            if (NumericSet.Contains(UnderlyingType))
            {
                BasicType = EOptionItemValueBasicType.Numeric;
                return;
            }
            if (DateTimeSet.Contains(UnderlyingType))
            {
                BasicType = EOptionItemValueBasicType.DateTime;
                return;
            }

            if (UnderlyingType.IsEnum)
            {
                BasicType = EOptionItemValueBasicType.Enum;
                return;
            }


            if (UnderlyingType == typeof(bool))
            {
                BasicType = EOptionItemValueBasicType.Boolean;
                return;
            }

            if (UnderlyingType == typeof(TimeSpan))
            {
                BasicType = EOptionItemValueBasicType.TimeSpan;
                return;
            }
        }

        if (UnderlyingType.IsClass && UnderlyingType != typeof(string))
        {
            BasicType = EOptionItemValueBasicType.Object;
            return;
        }

        BasicType = EOptionItemValueBasicType.String;
        return;
    }

    #endregion

}
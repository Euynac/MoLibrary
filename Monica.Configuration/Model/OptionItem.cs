using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Monica.Configuration.Annotations;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Model;

/// <summary>
/// Configuration item metadata.
/// </summary>
public class OptionItem
{
   
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
                $"要求：每个配置项必须要写中文名称，有备注必须写备注，请负责各个配置类的人完善;Property {property.Name} of Type {property.DeclaringType?.FullName} is not tagged with {typeof(OptionSettingAttribute)}.");
        }
    }

    /// <summary>
    /// Option metadata attribute.
    /// </summary>
    public OptionSettingAttribute? Info { get; }
    /// <summary>
    /// Raw option property name.
    /// </summary>
    public string Name => PropertyInfo.Name;
    /// <summary>
    /// Display title of the option.
    /// </summary>
    public string Title => Info?.Title ?? PropertyInfo.Name;
    /// <summary>
    /// Option description.
    /// </summary>
    public string? Description => Info?.Title;
    /// <summary>
    /// Reflected property info for this option.
    /// </summary>
    public PropertyInfo PropertyInfo { get; }
    /// <summary>
    /// Basic option value type.
    /// </summary>
    public EOptionItemValueBasicType BasicType { get; set; }

    /// <summary>
    /// Underlying system type after removing wrappers such as nullable and generic collection types.
    /// </summary>
    public Type UnderlyingType { get; private set; } = null!;
    /// <summary>
    /// Special option type classification.
    /// </summary>
    public EOptionItemValueSpecialType? SpecialType { get; set; }
    /// <summary>
    /// Option key joined by ":" to stay consistent with native Configuration keys.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Option value.
    /// </summary>
    public object? Value { get; private set; }

    /// <summary>
    /// When the option value is a Dictionary, List, or nested configuration class,
    /// this contains the corresponding sub-configuration metadata.
    /// </summary>
    public MoConfiguration? SubConfigInfo { get; set; }

    #region Validate

    /// <summary>
    /// Validation regex pattern.
    /// </summary>
    public string? ValidateRegexPattern { get; set; }

    #endregion


    #region 来源信息

    /// <summary>
    /// Effective configuration provider name.
    /// </summary>
    public string? Provider => SourceList.LastOrDefault().Value;

    /// <summary>
    /// Effective configuration source details.
    /// </summary>
    public string? Source => SourceList.LastOrDefault().Key;

    /// <summary>
    /// All configuration sources. Later entries have higher precedence.
    /// </summary>
    public Dictionary<string, string> SourceList { get; } = new();
    public void SetSource(IConfigurationProvider provider, string sourceInfo)
    {
        SourceList.TryAdd(sourceInfo, provider.GetType().Name);
    }
    #endregion

    public void SetValueFromConfigInstance(object? instance)
    {
        Value = PropertyInfo.GetValue(instance);
        if (PropertyInfo.PropertyType is {IsGenericType: true} type)
        {
            if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GenericTypeArguments[1] is {IsClass:true} dictValueType && UtilsConfiguration.HasConfigAttribute(dictValueType))
            {
                SubConfigInfo = new MoConfiguration(dictValueType); // Nested configuration class inside dictionary value type.
            }
            else if (type.GetGenericTypeDefinition() == typeof(List<>) &&
                     type.GenericTypeArguments[0] is {IsClass: true} listValueType &&
                     UtilsConfiguration.HasConfigAttribute(listValueType))
            {
                SubConfigInfo = new MoConfiguration(listValueType); // Nested configuration class inside list item type.
            }
        }
        else if(PropertyInfo.PropertyType is {IsClass:true, IsArray:false} propertyType && propertyType != typeof(string))
        {
            SubConfigInfo = new MoConfiguration(propertyType); // Single nested configuration class.
        }
    }

    /// <summary>
    /// Creates option item metadata from a configuration type
    /// (only properties with setters are included).
    /// </summary>
    /// <param name="configType"></param>
    /// <param name="configInstance"></param>
    /// <param name="parentKey">Configuration section name.</param>
    /// <returns></returns>
    public static List<OptionItem> CreateItems(Type configType, object? configInstance, string? parentKey)
    {
        var items = configType.GetProperties().Where(p=>p.CanWrite)
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
    /// Generates validation regex based on property type and annotations.
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

    #region Type Normalization

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

                if (type.GetGenericArguments().ElementAt(0) is { } keyType && keyType != typeof(string))
                {
                    throw new Exception($"暂不支持的字典类型：{type.GetCleanFullName()}，目前Key类型必须是string");
                }
                UnderlyingType = type.GetGenericArguments().ElementAt(1);
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

using System.Reflection;
using Microsoft.Extensions.Options;
using MoLibrary.Configuration.Annotations;
using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Framework.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 配置使用方式枚举
/// </summary>
public enum EConfigurationUsageType
{
    /// <summary>
    /// 未知使用方式
    /// </summary>
    Unknown,
    /// <summary>
    /// 离线配置 - 使用 <see cref="IOptions{T}"/>
    /// </summary>
    Offline,
    /// <summary>
    /// 在线配置 - 使用 <see cref="IOptionsSnapshot{T}"/>
    /// </summary>
    OnlineSnapshot,
    /// <summary>
    /// 在线配置 - 使用 <see cref="IOptionsMonitor{T}"/>
    /// </summary>
    OnlineMonitor
}

/// <summary>
/// 配置类
/// </summary>
/// <param name="type"></param>
public class UnitConfiguration(Type type) : ProjectUnit(type, EProjectUnitType.Configuration), IHasProjectUnitFactory
{
    /// <summary>
    /// 配置依赖详情：记录哪些项目单元使用了此配置，以及它们对配置的使用方式
    /// </summary>
    public Dictionary<ProjectUnit, EConfigurationUsageType> ConfigurationDependencies { get; private set; } = new();

    /// <summary>
    /// 自动识别配置类是否为离线配置（根据依赖关系如果有一个使用了<see cref="IOptions{T}"/>，则为离线参数，如果都是在线类型则为在线参数，否则是未知）
    /// </summary>
    public bool? IsOffline => ConfigurationDependencies.Values.Any(v => v == EConfigurationUsageType.Offline) ? true : 
                              ConfigurationDependencies.Values.All(v => v is EConfigurationUsageType.OnlineSnapshot or EConfigurationUsageType.OnlineMonitor) && ConfigurationDependencies.Any() ? false : 
                              null;
    static UnitConfiguration()
    {
        AddUnitRegisterFactory(Factory);
        AddConstructorAnalyzerFactory(AnalyzerFactory);
    }

    private static ProjectUnit? AnalyzerFactory(ConstructorAnalysisContext context)
    {
        var parameterType = context.ParameterType;
        var dependentUnit = context.DependentUnit;
        
        if (!parameterType.IsInterface || !parameterType.IsImplementInterfaceGeneric(typeof(IOptions<>), out var genericTypeDefinition) ||
            genericTypeDefinition.GetGenericArguments().FirstOrDefault() is not { } configType) return null;
        
        if (!ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(configType.FullName ?? string.Empty,
                out var unit) || unit is not UnitConfiguration configurationUnit) return null;
        
        // 判断配置使用方式
        var usageType = EConfigurationUsageType.Unknown;
        if (parameterType.IsImplementInterfaceGeneric(typeof(IOptionsSnapshot<>)))
        {
            usageType = EConfigurationUsageType.OnlineSnapshot;
        }
        else if (parameterType.IsImplementInterfaceGeneric(typeof(IOptionsMonitor<>)))
        {
            usageType = EConfigurationUsageType.OnlineMonitor;
        }
        else
        {
            usageType = EConfigurationUsageType.Offline;
        }
        
        // 记录依赖关系和使用方式
        configurationUnit.ConfigurationDependencies[dependentUnit] = usageType;
            
        return configurationUnit;
    }

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.GetCustomAttribute<ConfigurationAttribute>() is {IsSubConfiguration: false};
    }

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            Postfix = "Options"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var unit = new UnitConfiguration(context.Type);

        unit = unit.VerifyType() ? unit : null;
        if (unit != null)
        {
            if (context.Type.GetCustomAttribute<ConfigurationAttribute>() is {} info)
            {
                unit.Title = info.Title ?? unit.Title;
                unit.Description = info.Description ?? unit.Description;
            }
        }


        return unit;
    }
}
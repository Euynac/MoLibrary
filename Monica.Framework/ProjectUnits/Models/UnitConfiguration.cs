using System.Reflection;
using Microsoft.Extensions.Options;
using Monica.Configuration.Annotations;
using Monica.Configuration.Models;
using Monica.Framework.ProjectUnits.Abstractions;
using Monica.Framework.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.ProjectUnits.Models;

/// <summary>
/// Configure usage enumeration
/// </summary>
public enum EConfigurationUsageType
{
    /// <summary>
    /// Unknown usage
    /// </summary>
    Unknown,
    /// <summary>
    /// Offline configuration - use <see cref="IOptions{T}"/>
    /// </summary>
    Offline,
    /// <summary>
    /// Online configuration - use <see cref="IOptionsSnapshot{T}"/>
    /// </summary>
    OnlineSnapshot,
    /// <summary>
    /// Online configuration - use <see cref="IOptionsMonitor{T}"/>
    /// </summary>
    OnlineMonitor
}

/// <summary>
/// Configuration class
/// </summary>
/// <param name="type"></param>
public class UnitConfiguration(Type type) : ProjectUnit(type, EProjectUnitType.Configuration), IHasProjectUnitFactory
{
    /// <summary>
    /// Gets the Monica.Configuration definition key that corresponds to this options type.
    /// </summary>
    public string DefinitionKey { get; private set; } = type.FullName ?? type.Name;

    /// <summary>
    /// Configuration dependency details: record which project units use this configuration and how they use it
    /// </summary>
    public Dictionary<ProjectUnit, EConfigurationUsageType> ConfigurationDependencies { get; private set; } = new();

    /// <summary>
    /// Automatically identify whether the configuration class is an offline configuration (according to the dependency relationship, if one uses <see cref="IOptions{T}"/>, it is an offline parameter, if they are both online types, it is an online parameter, otherwise it is unknown)
    /// </summary>
    public bool? IsOffline => ConfigurationDependencies.Values.Any(v => v == EConfigurationUsageType.Offline) ? true : 
                              ConfigurationDependencies.Values.All(v => v is EConfigurationUsageType.OnlineSnapshot or EConfigurationUsageType.OnlineMonitor) && ConfigurationDependencies.Any() ? false : 
                              null;

    /// <summary>
    /// Gets the reload behavior inferred from discovered options access patterns.
    /// </summary>
    public ConfigurationReloadBehavior? InferredReloadBehavior => IsOffline switch
    {
        true => ConfigurationReloadBehavior.RequiresRestart,
        false => ConfigurationReloadBehavior.OnlineReloadable,
        _ => null
    };
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
        
        if (!ProjectUnitRegistry.ProjectUnitsByFullName.TryGetValue(configType.FullName ?? string.Empty,
                out var unit) || unit is not UnitConfiguration configurationUnit) return null;
        
        // Determine configuration usage
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
        
        // Document dependencies and usage
        configurationUnit.ConfigurationDependencies[dependentUnit] = usageType;
            
        return configurationUnit;
    }

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.GetCustomAttribute<ConfigurationAttribute>() is not null;
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
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
                unit.DefinitionKey = info.DefinitionKey ?? context.Type.FullName ?? context.Type.Name;
                unit.Title = info.DisplayName ?? unit.Title;
                unit.Description = info.Description ?? unit.Description;
            }
        }


        return unit;
    }
}

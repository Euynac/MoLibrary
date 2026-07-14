using System.Reflection;
using Microsoft.Extensions.Options;
using Monica.Configuration.Annotations;
using Monica.Configuration.Models;
using Monica.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Models;

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
public class UnitConfiguration : ProjectUnit
{
    internal UnitConfiguration(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.Configuration, catalog)
    {
        DefinitionKey = type.FullName ?? type.Name;
    }

    /// <summary>
    /// Gets the Monica.Configuration definition key that corresponds to this options type.
    /// </summary>
    public string DefinitionKey { get; private set; }

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
    internal void RecordDependency(ProjectUnit dependentUnit, EConfigurationUsageType usageType)
    {
        ConfigurationDependencies[dependentUnit] = usageType;
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

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitConfiguration(type, catalog);

        unit = unit.VerifyType() ? unit : null;
        if (unit != null)
        {
            if (type.GetCustomAttribute<ConfigurationAttribute>() is {} info)
            {
                unit.DefinitionKey = info.DefinitionKey ?? type.FullName ?? type.Name;
                unit.Title = info.DisplayName ?? unit.Title;
                unit.Description = info.Description ?? unit.Description;
            }
        }


        return unit;
    }
}

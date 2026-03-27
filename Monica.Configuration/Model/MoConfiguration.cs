using Monica.Configuration.Annotations;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Model;

/// <summary>
/// Configuration type metadata.
/// </summary>
public class MoConfiguration
{
    /// <summary>
    /// Configuration CLR type.
    /// </summary>
    public Type ConfigType { get; }

    /// <summary>
    /// Configuration type name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Configuration key used for update operations.
    /// </summary>
    public string Key => Info.Section ?? Name;
    /// <summary>
    /// Option item metadata.
    /// </summary>
    public List<OptionItem> OptionItems { get; protected set; }

    /// <summary>
    /// Configuration attribute metadata.
    /// </summary>
    public ConfigurationAttribute Info { get; set; }

    /// <summary>
    /// Configuration version.
    /// </summary>
    public string Version { get; set; } = "";
    /// <summary>
    /// Project name that owns this configuration type.
    /// </summary>
    public string FromProjectName { get; set; }

    /// <summary>
    /// Default configuration file name.
    /// </summary>
    public string DefaultSourceFileName => $"{FromProjectName}.{Name}.json";
    public static MoConfiguration Create<T>(T config)
    {
        return new MoConfiguration(typeof(T), config);
    }

    public MoConfiguration(Type configType, object? configInstance = null)
    {
        ConfigType = configType;
        Name = configType.Name;
        Info = UtilsConfiguration.GetConfigAttribute(ConfigType)!;
        OptionItems = CreateOptionItems(configInstance);
        FromProjectName = configType.Assembly.GetName().Name ?? "Unknown";
    }

    private List<OptionItem> CreateOptionItems(object? configInstance)
    {
        return OptionItem.CreateItems(ConfigType, configInstance, Info.Section);
    }
  
    public void SetOptionValue(object? configInstance)
    {
        foreach (var item in OptionItems)
        {
            item.SetValueFromConfigInstance(configInstance);
        }
    }

    public override string ToString()
    {
        return OptionItems.Where(x => MoConfigurationManager.Setting.EnableLoggingWithoutOptionSetting || x.Info != null)
                .Select(p => $"[{Name}] {p}").StringJoin("\n")
                .BeNullIfWhiteSpace() ?? $"[{Name}] Can not get any options info";
    }
}

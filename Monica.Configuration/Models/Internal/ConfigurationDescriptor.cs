using Monica.Configuration.Annotations;
using Monica.Configuration.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Models.Internal;

/// <summary>
/// Configuration type metadata.
/// </summary>
public class ConfigurationDescriptor
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
    public static ConfigurationDescriptor Create<T>(T config)
    {
        return new ConfigurationDescriptor(typeof(T), config);
    }

    public ConfigurationDescriptor(Type configType, object? configInstance = null)
    {
        ConfigType = configType;
        Name = configType.Name;
        Info = ConfigurationTypeAccessor.GetConfigAttribute(ConfigType)!;
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
        return OptionItems.Where(x => ConfigurationRuntime.Setting.EnableLoggingWithoutOptionSetting || x.Info != null)
                .Select(p => $"[{Name}] {p}").StringJoin("\n")
                .BeNullIfWhiteSpace() ?? $"[{Name}] Can not get any options info";
    }
}

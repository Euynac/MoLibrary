using System.Text.Json.Serialization;
using Monica.Configuration.Providers.JsonFile;
using Monica.Core.JsonSerialization.Converters;
using Monica.Tool.Diagnostics;

namespace Monica.Configuration.Models;


public class ConfigurationOptionSnapshot
{
    /// <summary>
    /// Display title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Option name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Option key used for update operations.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Option description.
    /// </summary>
    public string? Desc { get; set; }
    /// <summary>
    /// Option value.
    /// </summary>
    [JsonConverter(typeof(PreserveOriginalWithEnumStringConverter))]
    public object? Value { get; set; }

    /// <summary>
    /// Whether the option is offline-only and requires service restart to take effect.
    /// </summary>
    public bool IsOffline { get; set; }

    /// <summary>
    /// Basic option value type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConfigurationValueKind Type { get; set; } = ConfigurationValueKind.String;
    /// <summary>
    /// Special option type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConfigurationSpecialValueKind? SpecialType { get; set; }

    /// <summary>
    /// Regex validation pattern.
    /// </summary>
    public string? RegexPattern { get; set; }

    /// <summary>
    /// Indicates whether this option is nullable (null is allowed).
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Nested sub-configuration structure.
    /// </summary>
    public ConfigurationSnapshot? SubStructure { get; set; }

    /// <summary>
    /// Effective configuration provider.
    /// </summary>
    public string? Provider { get; set; }
    /// <summary>
    /// Effective configuration source info.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// All configuration sources; later items have higher precedence
    /// (the last one is the effective source).
    /// </summary>
    [JsonIgnore]
    public List<ConfigurationSourceEntry>? SourceList { get; set; }
}

public class ConfigurationSnapshot
{

    /// <summary>
    /// Display title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Configuration type name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Configuration category.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Configuration description.
    /// </summary>
    public string? Desc { get; set; }

    /// <summary>
    /// Configuration items.
    /// </summary>
    public List<ConfigurationOptionSnapshot> Items { get; set; } = [];


    #region Audit Fields

    /// <summary>
    /// Version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Configuration fetch time.
    /// </summary>
    public DateTime FetchTime { get; } = DateTime.Now;
    /// <summary>
    /// Last configuration update time.
    /// </summary>
    public DateTime? LastModificationTime { get; set; }
    /// <summary>
    /// Last updater ID.
    /// </summary>
    public string? LastModifierId { get; set; }
    /// <summary>
    /// Last updater name.
    /// </summary>
    public string? Username { get; set; }

    #endregion

    /// <summary>
    /// Determines whether this configuration contains the specified option key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool ContainsKey(string key)
    {
        return Items.Any(p => p.Key.Equals(key));
    }

    /// <summary>
    /// Gets current configuration values as JSON.
    /// </summary>
    /// <returns></returns>
    public string ToJsonValue()
    {
        var configJson = new Dictionary<string, object?>();
        foreach (var item in Items)
        {
            configJson[item.Name] = item.Value;
        }

        return configJson.ToJsonString(customOptions: JsonFileConventions.JsonSerializerOptions) ?? "{}";
    }
}

public class ConfigurationServiceGroup
{
    /// <summary>
    /// Display title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Microservice name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// AppID
    /// </summary>
    public required string AppId { get; set; }

    /// <summary>
    /// Configuration classes under this microservice.
    /// </summary>
    public List<ConfigurationSnapshot> Children { get; set; } = [];
}

public class ConfigurationDomainGroup
{
    /// <summary>
    /// Display title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Domain name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Microservice information under this domain.
    /// </summary>
    public List<ConfigurationServiceGroup> Children { get; set; } = [];
}

/// <summary>
/// Configuration source metadata.
/// </summary>
public class ConfigurationSourceEntry
{
    /// <summary>
    /// Configuration provider type name.
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Configuration source details.
    /// </summary>
    public string? SourceInfo { get; set; }

    /// <summary>
    /// Indicates whether this is the effective source.
    /// </summary>
    public bool IsActive { get; set; }
}

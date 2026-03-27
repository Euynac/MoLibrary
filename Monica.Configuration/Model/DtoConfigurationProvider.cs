using System.ComponentModel.DataAnnotations;

namespace Monica.Configuration.Model;

/// <summary>
/// Configuration provider metadata.
/// </summary>
public class DtoConfigurationProvider
{
    /// <summary>
    /// Provider name.
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Provider type.
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Configuration key-value pairs.
    /// </summary>
    public Dictionary<string, string?> ConfigurationData { get; set; } = new();
}

/// <summary>
/// Grouped configuration provider metadata.
/// </summary>
public class DtoConfigurationProviderGroup
{
    /// <summary>
    /// Group name (grouped by provider type).
    /// </summary>
    [Required]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Providers within this group.
    /// </summary>
    public List<DtoConfigurationProvider> Providers { get; set; } = new();
}

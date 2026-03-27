namespace Monica.Configuration.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public class ConfigurationAttribute : Attribute
{
    /// <summary>
    /// Custom configuration section name used for JSON-based configuration binding.
    /// If empty, the type name is used as the default section name.
    /// </summary>
    public string? Section { get; internal set; }

    /// <summary>
    /// Disables section-based binding and treats this configuration type as isolated key-value options.
    /// TODO: File-based editing for isolated-node configuration is not supported yet.
    /// </summary>
    public bool DisableSection { get; set; }

    /// <summary>
    /// Indicates whether this configuration should be hidden from Dashboard UI. (Not implemented yet)
    /// <para>Dashboard hierarchy: Domain -> Service -> Configuration Type, with versioning at service-configuration level.</para>
    /// </summary>
    public bool HideFromDashboard { get; set; }

    /// <summary>
    /// Configuration display name shown on Dashboard.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Configuration category for Dashboard grouping, typically constrained by custom constants.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Configuration description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this is a sub-configuration dependent on a parent configuration.
    /// </summary>
    public bool IsSubConfiguration { get; set; }

    /// <summary>
    /// Indicates options under this configuration are offline-only and require restart to take effect.
    /// </summary>
    public bool IsOffline
    {
        get => _IsOffline ?? false;
        set => _IsOffline = value;
    }

    internal bool? _IsOffline { get; set; }
    /// <summary>
    /// Initializes a configuration attribute using type name as section name.
    /// </summary>
    public ConfigurationAttribute()
    {
    }

    /// <summary>
    /// Initializes a configuration attribute with an explicit section name.
    /// </summary>
    /// <param name="section"></param>
    public ConfigurationAttribute(string section)
    {
        Section = section;
    }

    // The following options map to official OptionBinder behavior.
    /// <summary>
    /// When false (the default), the binder will only attempt to set public properties.
    /// If true, the binder will attempt to set all non read-only properties.
    /// </summary>
    public bool? BindNonPublicProperties { get; set; }

    /// <summary>
    /// When false (the default), no exceptions are thrown when a configuration key is found for which the
    /// provided model object does not have an appropriate property which matches the key's name.
    /// When true, an <see cref="System.InvalidOperationException"/> is thrown with a description
    /// of the missing properties.
    /// </summary>
    public bool? ErrorOnUnknownConfiguration { get; set; }
}

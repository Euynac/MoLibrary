namespace Monica.Configuration.Annotations;

[AttributeUsage(AttributeTargets.Property)]
public class OptionSettingAttribute : Attribute
{
    public OptionSettingAttribute()
    {
        
    }

    public OptionSettingAttribute(string title)
    {
        Title = title;
    }

    /// <summary>
    /// Log format string. Use {0} as the value placeholder.
    /// </summary>
    public string? LoggingFormat { get; set; }

    /// <summary>
    /// Option title displayed on the Dashboard.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Option description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Marks the option as sensitive and therefore write-only in management surfaces.
    /// Sensitive options are masked in dashboards, history, diagnostics, and logs.
    /// This flag does not encrypt the underlying provider value at rest.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Marks the option as offline-only, meaning a service restart is required to apply changes.
    /// </summary>
    public bool IsOffline
    {
        get => _IsOffline ?? false;
        set => _IsOffline = value;
    }

    /// <summary>
    /// Backing field for <see cref="IsOffline"/>.
    /// </summary>
    internal bool? _IsOffline { get; set; }
}

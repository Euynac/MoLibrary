using Monica.Configuration.Models;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Configures startup effective-options reading.
/// </summary>
public sealed class MonicaEffectiveOptionsReaderOptions
{
    /// <summary>
    /// Gets or sets how Monica derives section paths for configuration types that do not set an explicit section path.
    /// </summary>
    /// <remarks>
    /// The default matches Monica bootstrap binding and the default Monica.Configuration module convention.
    /// </remarks>
    public ConfigurationSectionPathConvention SectionPathConvention { get; set; } =
        ConfigurationSectionPathConvention.ShortTypeName;

    /// <summary>
    /// Gets or sets whether the reader emits startup diagnostics.
    /// </summary>
    public bool Debugging { get; set; }
}

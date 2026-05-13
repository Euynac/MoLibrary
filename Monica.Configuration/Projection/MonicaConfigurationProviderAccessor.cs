namespace Monica.Configuration.Projection;

/// <summary>
/// Holds the active Monica configuration provider instance for reload coordination.
/// </summary>
internal sealed class MonicaConfigurationProviderAccessor
{
    /// <summary>
    /// Gets or sets the active provider.
    /// </summary>
    public MonicaConfigurationProvider? Provider { get; set; }
}

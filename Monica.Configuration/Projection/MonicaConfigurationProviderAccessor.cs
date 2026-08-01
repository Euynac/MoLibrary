namespace Monica.Configuration.Projection;

/// <summary>
/// Holds the active Monica configuration provider instance for reload coordination.
/// </summary>
internal sealed class MonicaConfigurationProviderAccessor
{
    /// <summary>
    /// Gets or sets the final application service provider used to resolve Monica configuration services.
    /// </summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Gets or sets the active provider.
    /// </summary>
    public MonicaConfigurationProvider? Provider { get; set; }

    /// <summary>
    /// Gets the monotonic revision of the active provider's last successful projection.
    /// </summary>
    public long SuccessfulProjectionRevision => Provider?.SuccessfulProjectionRevision ?? 0;
}

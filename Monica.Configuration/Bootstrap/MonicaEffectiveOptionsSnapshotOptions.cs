namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Configures startup effective-options snapshot loading.
/// </summary>
public sealed class MonicaEffectiveOptionsSnapshotOptions
{
    /// <summary>
    /// Gets or sets whether snapshot loading emits startup diagnostics.
    /// </summary>
    public bool Debugging { get; set; }
}

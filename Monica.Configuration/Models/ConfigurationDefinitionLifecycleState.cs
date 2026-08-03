namespace Monica.Configuration.Models;

/// <summary>
/// Describes whether a persisted configuration definition still has a current publisher.
/// </summary>
public enum ConfigurationDefinitionLifecycleState
{
    /// <summary>
    /// At least one logical publisher currently reports the definition, or the definition is registered by the
    /// current process.
    /// </summary>
    Active,

    /// <summary>
    /// The definition remains available for diagnostics and audit inspection, but no logical publisher currently
    /// reports it.
    /// </summary>
    Retired
}

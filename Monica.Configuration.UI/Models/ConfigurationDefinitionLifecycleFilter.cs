using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Selects which configuration-definition lifecycle states are shown in management lists.
/// </summary>
public enum ConfigurationDefinitionLifecycleFilter
{
    /// <summary>
    /// Shows definitions that still have a current publisher.
    /// </summary>
    Active,

    /// <summary>
    /// Shows definitions retained only for diagnosis and audit inspection.
    /// </summary>
    Retired,

    /// <summary>
    /// Shows active and retired definitions together.
    /// </summary>
    All
}

internal static class ConfigurationDefinitionLifecycleFilterExtensions
{
    public static bool Includes(
        this ConfigurationDefinitionLifecycleFilter filter,
        ConfigurationDefinitionLifecycleState lifecycleState)
    {
        return filter switch
        {
            ConfigurationDefinitionLifecycleFilter.Active =>
                lifecycleState == ConfigurationDefinitionLifecycleState.Active,
            ConfigurationDefinitionLifecycleFilter.Retired =>
                lifecycleState == ConfigurationDefinitionLifecycleState.Retired,
            ConfigurationDefinitionLifecycleFilter.All => true,
            _ => false
        };
    }
}

namespace Monica.Configuration.Models;

/// <summary>
/// Identifies the persistence unit used to page configuration mutation history.
/// </summary>
public enum ConfigurationHistoryUnitKind
{
    /// <summary>
    /// A standalone history row without a mutation-group identity.
    /// </summary>
    StandaloneHistory = 0,

    /// <summary>
    /// All matching history rows that share one mutation-group identity.
    /// </summary>
    MutationGroup = 1
}

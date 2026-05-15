namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores module enablement state for one Monica application instance.
/// </summary>
internal sealed class ModuleState
{
    /// <summary>
    /// Gets module types disabled during registration.
    /// </summary>
    public HashSet<Type> DisabledModuleTypes { get; } = [];

    /// <summary>
    /// Clears module enablement state.
    /// </summary>
    public void Clear()
    {
        DisabledModuleTypes.Clear();
    }
}

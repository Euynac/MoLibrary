using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores module registration lifecycle data for one Monica application instance.
/// </summary>
internal sealed class ModuleRegistryState
{
    /// <summary>
    /// Gets module registration errors captured during the current registration lifecycle.
    /// </summary>
    public List<ModuleRegistrationError> ModuleRegisterErrors { get; } = [];

    /// <summary>
    /// Gets module snapshots captured after successful registration.
    /// </summary>
    public List<ModuleRuntimeSnapshot> ModuleSnapshots { get; } = [];

    /// <summary>
    /// Gets registration information for every module type registered in this application instance.
    /// </summary>
    public Dictionary<Type, ModuleRegistrationState> ModuleRegisterContextDict { get; } = [];

    /// <summary>
    /// Clears all module registration lifecycle data.
    /// </summary>
    public void Clear()
    {
        ModuleRegisterErrors.Clear();
        ModuleSnapshots.Clear();
        ModuleRegisterContextDict.Clear();
    }
}

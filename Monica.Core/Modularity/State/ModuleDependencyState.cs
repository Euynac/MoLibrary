using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores module dependency graph data for one Monica application instance.
/// </summary>
internal sealed class ModuleDependencyState
{
    /// <summary>
    /// Gets mappings from module types to declared module keys.
    /// </summary>
    public Dictionary<Type, ModuleKey> ModuleTypeToKeyMap { get; } = [];

    /// <summary>
    /// Gets mappings from declared module keys to module types.
    /// </summary>
    public Dictionary<ModuleKey, Type> ModuleKeyToTypeDict { get; } = [];

    /// <summary>
    /// Gets the module dependency graph keyed by declaring module.
    /// </summary>
    public Dictionary<ModuleKey, HashSet<ModuleKey>> ModuleDependencyMap { get; } = [];

    /// <summary>
    /// Clears dependency graph data.
    /// </summary>
    public void Clear()
    {
        ModuleTypeToKeyMap.Clear();
        ModuleKeyToTypeDict.Clear();
        ModuleDependencyMap.Clear();
    }
}

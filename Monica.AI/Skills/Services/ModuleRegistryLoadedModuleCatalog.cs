using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Reads loaded module keys from Monica's module registry snapshot.
/// </summary>
public sealed class ModuleRegistryLoadedModuleCatalog : ILoadedModuleCatalog
{
    /// <inheritdoc />
    public IReadOnlySet<ModuleKey> GetLoadedModuleKeys()
    {
        return ModuleRegistry.ModuleSnapshots
            .Select(snapshot => snapshot.ModuleKey)
            .ToHashSet();
    }
}

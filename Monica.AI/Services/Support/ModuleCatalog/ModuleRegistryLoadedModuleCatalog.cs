using Monica.Core;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;

namespace Monica.AI.Services.Support.ModuleCatalog;

/// <summary>
/// Reads loaded module keys from Monica's module registry snapshot.
/// </summary>
internal sealed class ModuleRegistryLoadedModuleCatalog(MonicaApplication application) : ILoadedModuleCatalog
{
    /// <inheritdoc />
    public IReadOnlySet<ModuleKey> GetLoadedModuleKeys()
    {
        return application.Modules.RuntimeSnapshots
            .Select(snapshot => snapshot.ModuleKey)
            .ToHashSet();
    }
}

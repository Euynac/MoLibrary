using System.Collections.Frozen;
using Monica.Core;

namespace Monica.AI.Services.Support.ModuleCatalog;

/// <summary>
/// Reads loaded module strategy types from Monica's module registry snapshot.
/// </summary>
internal sealed class ModuleRegistryLoadedModuleCatalog(MonicaApplication application) : ILoadedModuleCatalog
{
    /// <inheritdoc />
    public IReadOnlySet<Type> GetLoadedModuleTypes()
    {
        return application.Modules.RuntimeSnapshots
            .Select(static snapshot => snapshot.ModuleType)
            .ToFrozenSet();
    }
}

using Monica.Core.Modularity.Models;

namespace Monica.AI.Services.Support.ModuleCatalog;

/// <summary>
/// Provides the set of modules loaded in the current Monica host.
/// </summary>
public interface ILoadedModuleCatalog
{
    /// <summary>
    /// Gets all loaded module keys.
    /// </summary>
    IReadOnlySet<ModuleKey> GetLoadedModuleKeys();
}

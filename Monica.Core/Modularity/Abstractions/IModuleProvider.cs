using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Interface for modules that provide implementation for another module.
/// Enables discovery of provider modules (e.g., Redis provides for StateStore).
/// </summary>
public interface IModuleProvider
{
    /// <summary>
    /// Gets the ModuleKey of the module this provider extends.
    /// </summary>
    ModuleKey ProvidesFor { get; }
}

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Interface for modules that provide implementation for another module.
/// Enables discovery of provider modules (e.g., Redis provides for StateStore).
/// </summary>
public interface IModuleProvider
{
    /// <summary>
    /// Gets the module CLR type whose provider family this module extends.
    /// </summary>
    Type ProvidesFor { get; }
}

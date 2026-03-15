using System.Reflection;

namespace Monica.Core.Module.TypeFinder;

/// <summary>
/// Defines a type finder used to discover types from related assemblies.
/// Typically used for automatic registration against business assemblies.
/// </summary>
public interface IDomainTypeFinder
{
    /// <summary>
    /// Finds all discovered types.
    /// </summary>
    /// <returns>The discovered types.</returns>
    IEnumerable<Type> GetTypes();

    /// <summary>
    /// Gets all related assemblies.
    /// </summary>
    /// <returns>The related assemblies.</returns>
    IEnumerable<Assembly> GetAssemblies();

    /// <summary>
    /// Gets the current assembly analysis snapshot for the type finder.
    /// </summary>
    TypeFinderAssemblyAnalysis GetAssemblyAnalysis();
    
    /// <summary>
    /// Type finder configuration options.
    /// </summary>
    /// <returns>The configuration options.</returns>
    ModuleCoreOptionTypeFinder Options { get; }
} 

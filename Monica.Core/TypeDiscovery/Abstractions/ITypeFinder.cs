using System.Reflection;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.TypeDiscovery.Abstractions;

/// <summary>
/// Defines a type finder used to discover types from related assemblies.
/// Typically used for automatic registration against business assemblies.
/// </summary>
public interface ITypeFinder
{
    /// <summary>
    /// Gets the stable type snapshot discovered for this host.
    /// </summary>
    /// <returns>A repeatable sequence whose order remains stable for the lifetime of this finder.</returns>
    /// <remarks>
    /// Implementations must cache assembly reflection. Enumerating the returned sequence more than once must not call
    /// <see cref="Assembly.GetTypes"/> again.
    /// </remarks>
    IEnumerable<Type> GetTypes();

    /// <summary>
    /// Gets the stable assembly snapshot used by this finder.
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
    TypeFinderOptions Options { get; }
} 

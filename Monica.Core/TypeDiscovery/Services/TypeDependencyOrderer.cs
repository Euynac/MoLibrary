using System.Reflection;
using Monica.Core.TypeDiscovery.Abstractions;
using Monica.Core.TypeDiscovery.Services.Support;

namespace Monica.Core.TypeDiscovery.Services;

/// <summary>
/// Provides deterministic dependency-first ordering over selected business types for one Monica host.
/// </summary>
internal sealed class TypeDependencyOrderer : ITypeDependencyOrderer
{
    private readonly TypeDependencyGraph _assemblyGraph;

    public TypeDependencyOrderer(IEnumerable<Assembly> assemblies)
    {
        _assemblyGraph = TypeDependencyGraph.Create(assemblies);
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> OrderDependencyFirst(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var typesByAssembly = types
            .DistinctBy(GetTypeIdentity)
            .GroupBy(static type => TypeDependencyGraph.GetAssemblyIdentity(type.Assembly), StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<Type>)group
                    .OrderBy(GetTypeIdentity, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        if (typesByAssembly.Count == 0)
        {
            return [];
        }

        return _assemblyGraph
            .OrderDependencyFirst(typesByAssembly.Keys)
            .SelectMany(assemblyIdentity => typesByAssembly[assemblyIdentity])
            .ToArray();
    }

    private static string GetTypeIdentity(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.AssemblyQualifiedName
               ?? throw new InvalidOperationException(
                   $"Type '{type.FullName}' does not have an assembly-qualified identity.");
    }
}

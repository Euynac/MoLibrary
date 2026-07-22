namespace Monica.Core.TypeDiscovery.Abstractions;

/// <summary>
/// Orders a selected set of discovered types according to dependencies between their owning assemblies.
/// </summary>
/// <remarks>
/// This is an opt-in view over the host's configured type-discovery assemblies. It does not reorder
/// <see cref="ITypeFinder.GetTypes()"/> or the shared business-type iterator stream. Dependencies are returned before
/// consumers, while independent assemblies and types use stable ordinal identities as tie breakers.
/// </remarks>
public interface ITypeDependencyOrderer
{
    /// <summary>
    /// Returns the distinct selected types in deterministic dependency-first order.
    /// </summary>
    /// <param name="types">
    /// Types owned by assemblies in the current host's configured type-discovery scope.
    /// </param>
    /// <returns>The dependency-first, deterministically ordered types.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a selected type is outside the configured scope or the selected assembly dependency graph contains
    /// a cycle.
    /// </exception>
    IReadOnlyList<Type> OrderDependencyFirst(IEnumerable<Type> types);
}

namespace Monica.Core.Modularity.Abstractions;

public interface IBusinessTypeIterator
{
    /// <summary>
    /// Processes the types discovered by the current global type finder.
    /// This hook is intended for host or business application types that participate in the configured scan.
    /// Reusable modules should not assume their own package or library assemblies will appear here unless the host explicitly includes them.
    /// Implementations must preserve the lazy pipeline by forwarding items with <c>yield return</c>.
    /// Do not materialize <paramref name="types"/> inside this method with <c>ToArray</c>, <c>ToList</c>, or similar APIs.
    /// A module may schedule isolated CPU work while this iterator is synchronously enumerated, but that work cannot
    /// target the already-passed <c>BeforeBusinessTypeIteration</c> startup-work barrier.
    /// </summary>
    /// <param name="types">The current sequence of business types.</param>
    /// <returns>The transformed sequence of business types.</returns>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types);
}

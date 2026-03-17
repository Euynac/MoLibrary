namespace Monica.Core.Modularity.Interfaces;

public interface IWantIterateBusinessTypes
{
    /// <summary>
    /// Processes the discovered business types.
    /// Implementations must preserve the lazy pipeline by forwarding items with <c>yield return</c>.
    /// Do not materialize <paramref name="types"/> inside this method with <c>ToArray</c>, <c>ToList</c>, or similar APIs.
    /// </summary>
    /// <param name="types">The current sequence of business types.</param>
    /// <returns>The transformed sequence of business types.</returns>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types);
}

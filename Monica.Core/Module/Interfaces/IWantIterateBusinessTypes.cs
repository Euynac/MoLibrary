namespace Monica.Core.Module.Interfaces;

public interface IWantIterateBusinessTypes
{
    /// <summary>
    /// Processes the discovered business types.
    /// </summary>
    /// <param name="types">The current sequence of business types.</param>
    /// <returns>The transformed sequence of business types.</returns>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types);
}

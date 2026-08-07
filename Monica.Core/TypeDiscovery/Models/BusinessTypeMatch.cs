namespace Monica.Core.TypeDiscovery.Models;

/// <summary>
/// Represents one business type selected by a structural <see cref="TypeQuery"/>.
/// </summary>
public sealed class BusinessTypeMatch
{
    internal BusinessTypeMatch(
        BusinessTypeShape shape,
        IReadOnlyList<OpenGenericInterfaceMatch> openGenericInterfaces)
    {
        Shape = shape;
        OpenGenericInterfaces = openGenericInterfaces;
    }

    /// <summary>
    /// Gets the matched runtime type.
    /// </summary>
    public Type Type => Shape.Type;

    /// <summary>
    /// Gets the shared, lazily cached structural facts for <see cref="Type"/>.
    /// </summary>
    public BusinessTypeShape Shape { get; }

    /// <summary>
    /// Gets open-generic interface matches contributed by the successful query branches.
    /// </summary>
    /// <remarks>
    /// The list is empty for queries without an <see cref="TypeQuery.ImplementsOpenGeneric(Type)"/> condition. For
    /// composite queries, it contains results only from successful positive branches; matches evaluated inside
    /// <see cref="TypeQuery.Not(TypeQuery)"/> are never exposed.
    /// </remarks>
    public IReadOnlyList<OpenGenericInterfaceMatch> OpenGenericInterfaces { get; }
}

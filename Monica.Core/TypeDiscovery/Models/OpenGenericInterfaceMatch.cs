namespace Monica.Core.TypeDiscovery.Models;

/// <summary>
/// Describes one closed interface through which a business type implements an open generic interface definition.
/// </summary>
public sealed class OpenGenericInterfaceMatch
{
    internal OpenGenericInterfaceMatch(Type implementationType, Type closedInterface)
    {
        ImplementationType = implementationType;
        ClosedInterface = closedInterface;
        GenericArguments = Array.AsReadOnly(closedInterface.GetGenericArguments());
    }

    /// <summary>
    /// Gets the discovered type that implements the interface.
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// Gets the closed constructed interface implemented by <see cref="ImplementationType"/>.
    /// </summary>
    public Type ClosedInterface { get; }

    /// <summary>
    /// Gets the generic arguments of <see cref="ClosedInterface"/> in declaration order.
    /// </summary>
    public IReadOnlyList<Type> GenericArguments { get; }
}

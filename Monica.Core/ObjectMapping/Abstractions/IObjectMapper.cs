using Mapster;

namespace Monica.Core.ObjectMapping.Abstractions;

/// <summary>
/// Defines object-to-object mapping operations backed by the configured mapping provider.
/// </summary>
public interface IObjectMapper
{
    /// <summary>
    /// Gets the underlying Mapster configuration used by the mapper.
    /// </summary>
    TypeAdapterConfig Config { get; }

    /// <summary>
    /// Converts an object to another. Creates a new object of <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TDestination">Type of the destination object</typeparam>
    /// <param name="source">Source object</param>
    TDestination Map<TDestination>(object source);

    /// <summary>
    /// Converts an object to another. Creates a new object of <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TDestination">Type of the destination object</typeparam>
    /// <typeparam name="TSource">Type of the source object</typeparam>
    /// <param name="source">Source object</param>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// Execute a mapping from the source object to the existing destination object
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TDestination">Destination type</typeparam>
    /// <param name="source">Source object</param>
    /// <param name="destination">Destination object</param>
    /// <returns>Returns the same <paramref name="destination"/> object after mapping operation</returns>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

    /// <summary>
    /// Execute a mapping from the source object to the existing destination object
    /// </summary>
    /// <param name="source">Source object</param>
    /// <param name="destination">Destination object</param>
    /// <param name="sourceType"></param>
    /// <param name="destinationType"></param>
    /// <returns>Returns the same <paramref name="destination"/> object after mapping operation</returns>
    object Map(object source, object destination, Type sourceType, Type destinationType);

    /// <summary>
    /// Execute a mapping from the source object to the existing destination object
    /// </summary>
    /// <param name="source">Source object</param>
    /// <param name="sourceType"></param>
    /// <param name="destinationType"></param>
    object Map(object source, Type sourceType, Type destinationType);

    /// <summary>
    /// Projects a queryable source to the destination type using the configured mapping rules.
    /// </summary>
    /// <typeparam name="TDestination">The destination element type.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The projected query.</returns>
    IQueryable<TDestination> ProjectToType<TDestination>(IQueryable source);
}

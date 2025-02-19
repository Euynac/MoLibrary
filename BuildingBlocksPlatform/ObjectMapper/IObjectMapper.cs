using MapsterMapper;


namespace BuildingBlocksPlatform.ObjectMapper;

public interface IObjectMapper
{

    /// <summary>
    /// Converts an object to another. Creates a new object of <see cref="TDestination"/>.
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
    /// <returns>Returns the same <see cref="destination"/> object after mapping operation</returns>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

    /// <summary>
    /// Execute a mapping from the source object to the existing destination object
    /// </summary>
    /// <param name="source">Source object</param>
    /// <param name="destination">Destination object</param>
    /// <param name="sourceType"></param>
    /// <param name="destinationType"></param>
    /// <returns>Returns the same <see cref="destination"/> object after mapping operation</returns>
    object Map(object source, object destination, Type sourceType, Type destinationType);

    /// <summary>
    /// Execute a mapping from the source object to the existing destination object
    /// </summary>
    /// <param name="source">Source object</param>
    /// <param name="sourceType"></param>
    /// <param name="destinationType"></param>
    object Map(object source, Type sourceType, Type destinationType);
}
public class DefaultObjectMapper(IMapper mapper) : IObjectMapper, ITransientDependency
{
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return mapper.Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return mapper.Map(source, destination);
    }

    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        return mapper.Map(source, destination, sourceType, destinationType);
    }

    public object Map(object source, Type sourceType, Type destinationType)
    {
        return mapper.Map(source, sourceType, destinationType);
    }
}

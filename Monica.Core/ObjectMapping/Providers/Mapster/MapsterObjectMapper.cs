using Mapster;
using MapsterMapper;

namespace Monica.Core.Features.MoMapper;

/// <summary>
/// Mapster-based implementation of <see cref="IObjectMapper" />.
/// </summary>
internal sealed class MapsterObjectMapper(IMapper mapper) : IObjectMapper
{
    public TypeAdapterConfig Config => mapper.Config;

    public TDestination Map<TDestination>(object source)
    {
        return mapper.Map<TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return mapper.Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return mapper.Map(source, destination);
    }

    public IQueryable<TDestination> ProjectToType<TDestination>(IQueryable source)
    {
        return source.ProjectToType<TDestination>(mapper.Config);
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

using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.DomainDrivenDesign.Interfaces;
using MapsterMapper;

namespace BuildingBlocksPlatform.DomainDrivenDesign;

public class MapsterProviderMoObjectMapper(IMapper mapper) : IMoObjectMapper, ITransientDependency
{
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return mapper.Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return mapper.Map(source, destination);
    }
}
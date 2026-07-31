using Mapster;
using MapsterMapper;
using Monica.Core.ObjectMapping.Abstractions;

namespace Monica.Core.ObjectMapping.Providers.Mapster;

/// <summary>
/// Mapster-based implementation of <see cref="IObjectMapper" />.
/// </summary>
/// <remarks>
/// A scoped mapper resolves the runtime configuration once per operation. This preserves Mapster's dependency-
/// injection context while allowing the host to publish a compiled configuration without mutating a configuration
/// that active mappings are using.
/// </remarks>
internal sealed class MapsterObjectMapper(
    IServiceProvider serviceProvider,
    MapsterConfigurationRuntime runtime) : IObjectMapper
{
    public TDestination Map<TDestination>(object source)
    {
        return CreateMapper().Map<TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return CreateMapper().Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return CreateMapper().Map(source, destination);
    }

    public IQueryable<TDestination> ProjectToType<TDestination>(IQueryable source)
    {
        return source.ProjectToType<TDestination>(runtime.CurrentConfiguration);
    }

    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        return CreateMapper().Map(source, destination, sourceType, destinationType);
    }

    public object Map(object source, Type sourceType, Type destinationType)
    {
        return CreateMapper().Map(source, sourceType, destinationType);
    }

    private ServiceMapper CreateMapper()
    {
        return new ServiceMapper(serviceProvider, runtime.CurrentConfiguration);
    }
}

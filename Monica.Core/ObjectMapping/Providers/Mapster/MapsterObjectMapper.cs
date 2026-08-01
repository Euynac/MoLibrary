using Mapster;
using MapsterMapper;
using Monica.Core.ObjectMapping.Abstractions;

namespace Monica.Core.ObjectMapping.Providers.Mapster;

/// <summary>
/// Mapster-based implementation of <see cref="IObjectMapper" />.
/// </summary>
/// <remarks>
/// A scoped mapper caches the mapper for the current runtime configuration and replaces it only after the host
/// publishes a compiled configuration. This preserves Mapster's dependency-injection context without allocating a
/// mapper per operation or mutating a configuration that active mappings are using.
/// </remarks>
internal sealed class MapsterObjectMapper(
    IServiceProvider serviceProvider,
    MapsterConfigurationRuntime runtime) : IObjectMapper
{
    private readonly Lock _mapperLock = new();
    private ServiceMapper _mapper = new(serviceProvider, runtime.CurrentConfiguration);

    public TDestination Map<TDestination>(object source)
    {
        return GetCurrentMapper().Map<TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return GetCurrentMapper().Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return GetCurrentMapper().Map(source, destination);
    }

    public IQueryable<TDestination> ProjectToType<TDestination>(IQueryable source)
    {
        return source.ProjectToType<TDestination>(runtime.CurrentConfiguration);
    }

    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        return GetCurrentMapper().Map(source, destination, sourceType, destinationType);
    }

    public object Map(object source, Type sourceType, Type destinationType)
    {
        return GetCurrentMapper().Map(source, sourceType, destinationType);
    }

    internal ServiceMapper GetCurrentMapper()
    {
        var configuration = runtime.CurrentConfiguration;
        var mapper = Volatile.Read(ref _mapper);
        if (ReferenceEquals(mapper.Config, configuration))
        {
            return mapper;
        }

        lock (_mapperLock)
        {
            // Publication is monotonic, but another caller may already have refreshed this scoped mapper.
            configuration = runtime.CurrentConfiguration;
            mapper = _mapper;
            if (ReferenceEquals(mapper.Config, configuration))
            {
                return mapper;
            }

            mapper = new ServiceMapper(serviceProvider, configuration);
            Volatile.Write(ref _mapper, mapper);
            return mapper;
        }
    }
}

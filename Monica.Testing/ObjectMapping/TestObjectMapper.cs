using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.ObjectMapping.Abstractions;

namespace Monica.Testing.ObjectMapping;

/// <summary>
/// Lightweight Mapster-backed mapper for tests that need Monica's object-mapping abstraction without booting the module.
/// </summary>
public sealed class TestObjectMapper : IObjectMapper
{
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a mapper over an explicit Mapster configuration.
    /// </summary>
    public TestObjectMapper(TypeAdapterConfig? config = null)
        : this(new Mapper(config ?? new TypeAdapterConfig()))
    {
    }

    /// <summary>
    /// Initializes a mapper over Mapster's service-aware mapper.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public TestObjectMapper(IMapper mapper)
    {
        _mapper = mapper;
    }

    /// <inheritdoc />
    public TDestination Map<TDestination>(object source)
    {
        return _mapper.Map<TDestination>(source);
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return _mapper.Map<TSource, TDestination>(source);
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return _mapper.Map(source, destination);
    }

    /// <inheritdoc />
    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        return _mapper.Map(source, destination, sourceType, destinationType);
    }

    /// <inheritdoc />
    public object Map(object source, Type sourceType, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _mapper.Map(source, sourceType, destinationType);
    }

    /// <inheritdoc />
    public IQueryable<TDestination> ProjectToType<TDestination>(IQueryable source)
    {
        return source.ProjectToType<TDestination>(_mapper.Config);
    }
}

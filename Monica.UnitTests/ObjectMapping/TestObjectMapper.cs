using Mapster;
using Monica.Core.ObjectMapping.Abstractions;

namespace Monica.UnitTests.ObjectMapping;

/// <summary>
/// Lightweight Mapster-backed mapper for tests that need Monica's object-mapping abstraction without booting the module.
/// </summary>
public sealed class TestObjectMapper(TypeAdapterConfig? config = null) : IObjectMapper
{
    /// <inheritdoc />
    public TypeAdapterConfig Config { get; } = config ?? new TypeAdapterConfig();

    /// <inheritdoc />
    public TDestination Map<TDestination>(object source)
    {
        return source.Adapt<TDestination>(Config);
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        return source.Adapt<TSource, TDestination>(Config);
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        return source.Adapt(destination, Config);
    }

    /// <inheritdoc />
    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        return source.Adapt(destination, Config);
    }

    /// <inheritdoc />
    public object Map(object source, Type sourceType, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Adapt(destinationType, Config)
            ?? throw new InvalidOperationException($"Mapster returned null for destination type {destinationType.FullName}.");
    }

    /// <inheritdoc />
    public IQueryable<TDestination> ProjectToType<TDestination>(IQueryable source)
    {
        return source.ProjectToType<TDestination>(Config);
    }
}

using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.Abstractions;

namespace Monica.Testing.ProjectUnits;

/// <summary>
/// Owns a scoped ProjectUnit created by a raw fast-path test container.
/// </summary>
/// <typeparam name="TUnit">The ProjectUnit implementation under test.</typeparam>
public sealed class ProjectUnitFixture<TUnit> : IDisposable, IAsyncDisposable
    where TUnit : class
{
    private readonly ServiceProvider _rootProvider;
    private readonly AsyncServiceScope _scope;
    private bool _disposed;

    internal ProjectUnitFixture(
        ServiceProvider rootProvider,
        AsyncServiceScope scope,
        TUnit unit,
        ICachedServiceProvider cachedServiceProvider)
    {
        _rootProvider = rootProvider;
        _scope = scope;
        Unit = unit;
        Services = scope.ServiceProvider;
        CachedServiceProvider = cachedServiceProvider;
    }

    /// <summary>
    /// Gets the ProjectUnit instance under test.
    /// </summary>
    public TUnit Unit { get; }

    /// <summary>
    /// Gets the scoped service provider that created the ProjectUnit instance.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the Monica cached service provider assigned to the unit when the unit supports it.
    /// </summary>
    public ICachedServiceProvider CachedServiceProvider { get; }

    /// <summary>
    /// Creates a builder for an arbitrary ProjectUnit.
    /// </summary>
    public static ProjectUnitFixtureBuilder<TUnit> Builder() => new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _scope.DisposeAsync();
        await _rootProvider.DisposeAsync();
    }
}

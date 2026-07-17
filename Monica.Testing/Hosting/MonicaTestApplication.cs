using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Testing.Hosting;

/// <summary>
/// Owns one fully started Monica test host and the module composition bound to that host.
/// </summary>
public sealed class MonicaTestApplication : IAsyncDisposable
{
    private readonly WebApplication _host;
    private bool _disposed;

    internal MonicaTestApplication(WebApplication host)
    {
        _host = host;
        Application = host.Services.GetRequiredService<MonicaApplication>();
        ModuleSnapshots = Array.AsReadOnly(Application.Modules.RuntimeSnapshots.ToArray());
    }

    /// <summary>
    /// Gets the root service provider owned by this scenario host.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Gets the Monica composition owned by this scenario host.
    /// </summary>
    public MonicaApplication Application { get; }

    /// <summary>
    /// Gets the immutable module snapshot captured after host startup.
    /// </summary>
    public IReadOnlyList<ModuleRuntimeSnapshot> ModuleSnapshots { get; }

    /// <summary>
    /// Creates a service scope owned by this scenario host.
    /// </summary>
    /// <param name="cancellationToken">The token test operations in the scope should observe.</param>
    /// <returns>A scope that resolves services only from this application's service graph.</returns>
    public MonicaTestScope CreateScope(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new MonicaTestScope(_host.Services.CreateAsyncScope(), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await _host.StopAsync();
        }
        finally
        {
            await _host.DisposeAsync();
        }
    }
}

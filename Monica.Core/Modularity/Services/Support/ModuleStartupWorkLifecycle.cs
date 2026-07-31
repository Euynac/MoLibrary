using Microsoft.Extensions.Hosting;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Retains non-blocking startup work under Generic Host ownership until shutdown completes.
/// </summary>
internal sealed class ModuleStartupWorkLifecycle(MonicaApplication application) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        application.Modules.DrainStartupWork();
        return Task.CompletedTask;
    }
}

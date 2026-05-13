using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;

namespace Monica.Configuration.EfCore.Services;

/// <summary>
/// Publishes definitions from the current owner service at startup.
/// </summary>
public sealed class ConfigurationDefinitionPublishingHostedService(
    IConfigurationDefinitionRegistry registry,
    IServiceScopeFactory scopeFactory)
    : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IConfigurationDefinitionPublisher>();
        await publisher.PublishAsync(registry.GetAll(), cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

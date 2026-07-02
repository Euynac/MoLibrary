using Microsoft.Extensions.Hosting;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Connects the module-owned Microsoft configuration provider to the final application service provider.
/// </summary>
internal sealed class MonicaConfigurationProviderActivationHostedService(MonicaConfigurationProviderActivationCoordinator activationCoordinator)
    : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await activationCoordinator.ActivateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

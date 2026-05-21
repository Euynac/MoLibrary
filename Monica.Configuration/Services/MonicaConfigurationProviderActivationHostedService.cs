using Microsoft.Extensions.Hosting;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Projection;

namespace Monica.Configuration.Services;

/// <summary>
/// Connects the module-owned Microsoft configuration provider to the final application service provider.
/// </summary>
internal sealed class MonicaConfigurationProviderActivationHostedService(
    IServiceProvider serviceProvider,
    MonicaConfigurationProviderAccessor accessor,
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationMetadataStore metadataStore,
    IConfigurationStoreStateTracker stateTracker,
    IConfigurationReloadCoordinator reloadCoordinator)
    : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        accessor.ServiceProvider = serviceProvider;
        try
        {
            await metadataStore.PublishAsync(definitionRegistry.GetAll(), cancellationToken);
            stateTracker.RecordSuccess(metadataStore.Descriptor.StoreKey);
        }
        catch (Exception ex)
        {
            stateTracker.RecordFailure(metadataStore.Descriptor.StoreKey, ex);
            throw;
        }

        await reloadCoordinator.ReloadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

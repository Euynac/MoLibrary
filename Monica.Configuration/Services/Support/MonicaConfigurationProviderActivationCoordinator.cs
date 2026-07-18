using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Idempotently connects Monica's configuration projection to the final application service provider.
/// </summary>
internal sealed class MonicaConfigurationProviderActivationCoordinator(
    IServiceProvider serviceProvider,
    MonicaConfigurationProviderAccessor accessor,
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationMetadataStore metadataStore,
    ConfigurationPublisherIdentityProvider publisherIdentityProvider,
    ConfigurationDefinitionResolver definitionResolver,
    IConfigurationStoreStateTracker stateTracker,
    IConfigurationReloadCoordinator reloadCoordinator)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _activated;

    /// <summary>
    /// Activates the projection provider and publishes local configuration metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        if (_activated)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activated)
            {
                return;
            }

            accessor.ServiceProvider = serviceProvider;
            try
            {
                var publication = ConfigurationDefinitionPublicationBatch.Create(
                    publisherIdentityProvider.GetIdentity(),
                    definitionRegistry.GetAll());
                await metadataStore.PublishAsync(publication, cancellationToken);
                definitionResolver.InvalidateReadSnapshot();
                stateTracker.RecordSuccess(metadataStore.Descriptor.StoreKey);
            }
            catch (Exception ex)
            {
                stateTracker.RecordFailure(metadataStore.Descriptor.StoreKey, ex);
                throw;
            }

            await reloadCoordinator.ReloadMonicaProjectionAsync(cancellationToken);
            _activated = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

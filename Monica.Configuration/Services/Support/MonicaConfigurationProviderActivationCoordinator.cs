using System.Runtime.ExceptionServices;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Metrics;
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
    IConfigurationEffectiveValueStore effectiveValueStore,
    ConfigurationPublisherIdentityProvider publisherIdentityProvider,
    ConfigurationDefinitionResolver definitionResolver,
    IConfigurationStoreStateTracker stateTracker,
    IConfigurationReloadCoordinator reloadCoordinator,
    ConfigurationMetricsRecorder metricsRecorder)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _activated;

    /// <summary>
    /// Activates the projection provider and publishes local configuration metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activated)
            {
                return;
            }

            accessor.ServiceProvider = serviceProvider;
            await metricsRecorder.MeasureStartupStageAsync(
                ConfigurationStartupStage.ProviderActivation,
                () => ActivateProviderAsync(cancellationToken));
            _activated = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ActivateProviderAsync(CancellationToken cancellationToken)
    {
        var publicationTask = CaptureStageFailureAsync(
            ConfigurationStartupStage.MetadataPublication,
            () => PublishMetadataAsync(cancellationToken));
        var projectionTask = CaptureStageFailureAsync(
            ConfigurationStartupStage.ProjectionReload,
            () => reloadCoordinator.ReloadMonicaProjectionAsync(cancellationToken));
        var failures = await Task.WhenAll(publicationTask, projectionTask);
        var publicationFailure = failures[0];
        var projectionFailure = failures[1];

        if (publicationFailure is not null)
        {
            stateTracker.RecordFailure(metadataStore.Descriptor.StoreKey, publicationFailure);
        }
        else if (projectionFailure is null
                 || !string.Equals(
                     metadataStore.Descriptor.StoreKey,
                     effectiveValueStore.Descriptor.StoreKey,
                     StringComparison.Ordinal))
        {
            // A shared store key must retain a projection failure. Independent stores can publish their own health.
            stateTracker.RecordSuccess(metadataStore.Descriptor.StoreKey);
        }

        ThrowIfFailed(failures);
    }

    private async Task<Exception?> CaptureStageFailureAsync(
        ConfigurationStartupStage stage,
        Func<Task> operation)
    {
        try
        {
            await metricsRecorder.MeasureStartupStageAsync(stage, operation);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async Task PublishMetadataAsync(CancellationToken cancellationToken)
    {
        var publication = ConfigurationDefinitionPublicationBatch.Create(
            publisherIdentityProvider.GetIdentity(),
            definitionRegistry.GetAll());
        await metadataStore.PublishAsync(publication, cancellationToken);
        definitionResolver.InvalidateReadSnapshot();
    }

    private static void ThrowIfFailed(IReadOnlyList<Exception?> stageFailures)
    {
        var failures = stageFailures.OfType<Exception>().ToArray();
        if (failures.Length == 0)
        {
            return;
        }

        var faults = failures
            .Where(static exception => exception is not OperationCanceledException)
            .ToArray();
        if (faults.Length == 1)
        {
            ExceptionDispatchInfo.Capture(faults[0]).Throw();
        }

        if (faults.Length > 1)
        {
            throw new AggregateException("Configuration provider activation failed in multiple concurrent stages.", faults);
        }

        ExceptionDispatchInfo.Capture(failures[0]).Throw();
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Publishes a worker project's complete discovered job-definition snapshot to the central scheduler control plane.
/// </summary>
/// <remarks>
/// Publication is repeated as an anti-entropy mechanism. Delivery may therefore be duplicated, and the registry
/// reconciler is responsible for idempotency and deployment-version ordering.
/// </remarks>
internal sealed class JobDefinitionPublisherHostedService(
    IReadOnlyList<JobDefinition> localDefinitions,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    IServiceDiscoveryClientInfo clientInfo,
    IServiceRegistrationCoordinator registrationCoordinator,
    IOptions<ModuleJobSchedulerOption> options,
    IOptions<ModuleServiceDiscoveryOption> serviceDiscoveryOptions,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobDefinitionPublisherHostedService> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, serviceScopeFactory, logger)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;
    private readonly ModuleServiceDiscoveryOption _serviceDiscoveryOptions = serviceDiscoveryOptions.Value;

    public override string? ServiceGroupId => nameof(ModuleJobScheduler);

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        await WaitForRegistrationAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextPublicationDelay = _options.DefinitionPublicationInterval;
            try
            {
                var service = clientInfo.GetServiceStatus(isHeartbeatInfo: false);
                var snapshot = JobDefinitionSnapshotPublishedEvent.Create(
                    _options.SchedulerScopeKey,
                    _options.GetProjectName(),
                    service.AppId,
                    service.InstanceId,
                    service.BuildTime,
                    service.ReleaseVersion,
                    localDefinitions);

                await eventBus.PublishAsync(
                    snapshot,
                    JobEventTopicHelper.GetTopicName<JobDefinitionSnapshotPublishedEvent>(
                        _options.SchedulerScopeKey),
                    stoppingToken);

                RecordState(
                    $"Published job-definition snapshot {snapshot.ContentHash} containing " +
                    $"{snapshot.Definitions.Count} definition(s)",
                    HostedServiceState.Running,
                    logLevel: LogLevel.Information);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                nextPublicationDelay = _options.DefinitionPublicationRetryInterval;
                RecordState(
                    "Job-definition snapshot publication failed; retrying in " +
                    $"{nextPublicationDelay}",
                    HostedServiceState.Degraded,
                    exception,
                    LogLevel.Error);
            }

            await Task.Delay(nextPublicationDelay, stoppingToken);
        }
    }

    private async Task WaitForRegistrationAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            RecordState(
                "Waiting for service registration before publishing job definitions",
                HostedServiceState.WaitingDependency,
                logLevel: LogLevel.Information);

            if (await registrationCoordinator.WaitForRegistrationAsync(
                    _serviceDiscoveryOptions.RegistrationWaitTimeout,
                    cancellationToken))
            {
                return;
            }

            RecordState(
                $"Service registration is not ready; retrying job-definition publication in " +
                $"{_options.DefinitionPublicationRetryInterval}",
                HostedServiceState.Degraded,
                logLevel: LogLevel.Warning);
            await Task.Delay(_options.DefinitionPublicationRetryInterval, cancellationToken);
        }
    }
}

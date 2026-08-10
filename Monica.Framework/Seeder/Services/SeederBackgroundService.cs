using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Services.Support;
using Monica.Modules;

namespace Monica.Framework.Seeder.Services;

internal sealed class SeederBackgroundService(
    IHostApplicationLifetime applicationLifetime,
    SeederScheduler scheduler,
    SeederState seederState,
    IObservableInstanceRegistry observableRegistry,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SeederBackgroundService> logger)
    : MoBackgroundService(observableRegistry, hostedServiceOptions, serviceScopeFactory, logger)
{
    public override string ServiceName => nameof(SeederBackgroundService);

    public override string? ServiceGroupId => nameof(ModuleSeeder);

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState(
            "Waiting for the application to finish starting before scheduling seeders.",
            HostedServiceState.WaitingDependency);
        await WaitForApplicationStartedAsync(applicationLifetime.ApplicationStarted, stoppingToken)
            .ConfigureAwait(false);

        RecordState("Scheduling the seeder dependency graph.", HostedServiceState.Executing);
        await scheduler.RunAsync(stoppingToken).ConfigureAwait(false);

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var snapshot = seederState.GetSnapshot();
        var requiredFailures = snapshot.Seeders.Count(static seeder =>
            seeder.Criticality == SeederCriticality.Required && seeder.Status != SeederStatus.Succeeded);
        var optionalFailures = snapshot.Seeders.Count(static seeder =>
            seeder.Criticality == SeederCriticality.Optional &&
            seeder.Status is SeederStatus.Failed or SeederStatus.Blocked or SeederStatus.Cancelled);

        if (requiredFailures > 0)
        {
            RecordState(
                $"Seeder run completed with {requiredFailures} unsuccessful required seeders.",
                HostedServiceState.Faulted);
        }
        else if (optionalFailures > 0)
        {
            RecordState(
                $"Seeder run completed with {optionalFailures} unsuccessful optional seeders.",
                HostedServiceState.Degraded);
        }
        else
        {
            RecordState(
                $"Seeder run completed successfully for {snapshot.Seeders.Length} seeders.",
                HostedServiceState.Running);
        }
    }

    private static async Task WaitForApplicationStartedAsync(
        CancellationToken applicationStarted,
        CancellationToken stoppingToken)
    {
        if (applicationStarted.IsCancellationRequested)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = applicationStarted.Register(static state =>
            ((TaskCompletionSource)state!).TrySetResult(), completion);
        await completion.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Services;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using NSubstitute;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobDispatcherCancellationTests
{
    [Fact]
    public async Task PublishJobExecutionEventAsync_WhenDeliveryIsCancelled_ReleasesReservationAndRethrows()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var eventBus = Substitute.For<IEventBus>();
        var concurrencyGuard = Substitute.For<IJobConcurrencyGuard>();
        var metadataRepository = Substitute.For<IJobMetadataRepository>();
        concurrencyGuard
            .TryReserveExecutionSlotAsync("jobs.alpha", "instance-1", cancellation.Token)
            .Returns(ReservationResult.Success());
        eventBus
            .PublishAsync(
                Arg.Any<JobExecutionEvent>(),
                Arg.Any<string?>(),
                cancellation.Token)
            .Returns(Task.FromCanceled(cancellation.Token));

        var dispatcher = new JobDispatcher(
            eventBus,
            NullLogger<JobDispatcher>.Instance,
            CreateJobInstanceManager(eventBus, metadataRepository),
            concurrencyGuard);
        var definition = new JobDefinition
        {
            SchedulerScopeKey = "scope-a",
            JobKey = "jobs.alpha",
            FromProject = "Test.Project",
            JobName = "Alpha Job",
            JobType = JobType.Triggered
        };
        var instance = new JobInstance
        {
            SchedulerScopeKey = definition.SchedulerScopeKey,
            InstanceId = "instance-1",
            JobKey = definition.JobKey,
            State = JobState.Enqueued
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.PublishJobExecutionEventAsync(
                instance,
                definition,
                jobArgsJson: null,
                cancellation.Token));

        await concurrencyGuard.Received(1).ReleaseReservedSlotAsync(
            definition.JobKey,
            instance.InstanceId,
            CancellationToken.None);
        Assert.Empty(metadataRepository.ReceivedCalls());
    }

    private static JobInstanceManager CreateJobInstanceManager(
        IEventBus eventBus,
        IJobMetadataRepository metadataRepository)
    {
        return new JobInstanceManager(
            metadataRepository,
            eventBus,
            Substitute.For<IServiceDiscoveryClientInfo>(),
            Options.Create(new ModuleJobSchedulerOption()),
            NullLogger<JobInstanceManager>.Instance);
    }
}

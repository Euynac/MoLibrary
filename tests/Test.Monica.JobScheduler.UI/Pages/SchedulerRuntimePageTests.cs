using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Monica.JobScheduler.UI.Pages;
using Monica.JobScheduler.UI.UIJobScheduler.Abstractions;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Pages;

public sealed class SchedulerRuntimePageTests
{
    [Fact]
    public async Task RuntimePage_WithoutInsightProvider_ShouldRenderAllSectionsAndTheSetupHint()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<SchedulerRuntimePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Runtime:Identity:Title");
            cut.Markup.Should().Contain("Runtime:Config:Title");
            cut.Markup.Should().Contain("Runtime:Plane:Title");
            cut.Markup.Should().Contain("Runtime:Owners:Title");
            cut.Markup.Should().Contain("Runtime:Owners:JobsCounts");
            cut.Markup.Should().Contain(JobSchedulerUiTestContext.OWNER);
            cut.Markup.Should().Contain("Runtime:Workers:Title");
            cut.Markup.Should().Contain("Runtime:Workers:Hint");
            cut.Markup.Should().NotContain("Runtime:Workers:Empty");
        });
    }

    [Fact]
    public async Task RuntimePage_WithInsightProvider_ShouldRenderWorkerInstancesInsteadOfTheHint()
    {
        var provider = new StaticWorkerInsightProvider(
        [
            new JobSchedulerWorkerInstanceView
            {
                AppId = "fips-system",
                AppName = "SystemService.API",
                ProjectName = JobSchedulerUiTestContext.OWNER,
                InstanceId = "insight-worker-1",
                IsLeader = true,
                RegisteredAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-2),
                Status = JobSchedulerWorkerInstanceStatus.Online,
                ListeningAddresses = ["http://localhost:5000"]
            }
        ]);
        await using var context = new JobSchedulerUiTestContext(workerInsight: provider);

        var cut = context.Render<SchedulerRuntimePage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("insight-worker-1");
            cut.Markup.Should().Contain("http://localhost:5000");
            cut.Markup.Should().NotContain("Runtime:Workers:Hint");
        });
        provider.Requested.Should().BeTrue();
    }

    [Fact]
    public async Task RuntimeState_WithoutInsightProvider_ShouldExposeTheOverviewWithoutWorkers()
    {
        await using var context = new JobSchedulerUiTestContext();
        await using var state = context.Services
            .GetRequiredService<SchedulerRuntimePageStateFactory>()
            .Create(TimeSpan.Zero);

        await state.InitializeAsync();

        state.AccessChecked.Should().BeTrue();
        state.IsAuthorized.Should().BeTrue();
        state.HasWorkerInsight.Should().BeFalse();
        state.Workers.Should().BeEmpty();
        state.Overview.Should().NotBeNull();
        state.Overview!.Store.Kind.Should().Be("InMemory");
        state.Overview.Owners.Should().ContainSingle()
            .Which.OwnerKey.Should().Be(JobSchedulerUiTestContext.OWNER);
    }

    private sealed class StaticWorkerInsightProvider(IReadOnlyList<JobSchedulerWorkerInstanceView> instances)
        : IJobSchedulerWorkerInsightProvider
    {
        public bool Requested { get; private set; }

        public Task<IReadOnlyList<JobSchedulerWorkerInstanceView>> GetWorkerInstancesAsync(
            CancellationToken cancellationToken = default)
        {
            Requested = true;
            return Task.FromResult(instances);
        }
    }
}

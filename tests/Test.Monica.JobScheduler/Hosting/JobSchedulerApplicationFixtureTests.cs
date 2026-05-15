using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.Modules;
using Monica.StateStore.Cancellation.Abstractions;
using Monica.UnitTests.Hosting;
using Xunit;

namespace Test.Monica.JobScheduler.Hosting;

public sealed class JobSchedulerApplicationFixture : MonicaApplicationFixture<ModuleJobScheduler>
{
    public const string SchedulerScope = "job-sociable-tests";
    private const string PROJECT_NAME = "Test.Monica.JobScheduler";

    protected override void ConfigureModule(IHostApplicationBuilder builder)
    {
        new ModuleJobSchedulerGuide()
            .Register(options =>
            {
                options.ProjectName = PROJECT_NAME;
                options.RecurringJobDebugMode = true;
                options.TriggeredJobDebugMode = true;
                options.EnableLongIntervalScheduler = false;
                options.EnableZombieDetection = false;
                options.EnableHistoryCleanup = false;
            })
            .UseSchedulerScope(SchedulerScope)
            .UseInMemoryProvider()
            .UseInMemoryMetadataRepository();
    }
}

public sealed class JobSchedulerApplicationFixtureTests(JobSchedulerApplicationFixture fixture)
    : IClassFixture<JobSchedulerApplicationFixture>
{
    [Fact]
    public async Task NewScope_WhenModuleGraphBoots_ShouldResolveConfiguredJobSchedulerServices()
    {
        await using var scope = await fixture.NewScopeAsync();

        var options = scope.Resolve<IOptions<ModuleJobSchedulerOption>>().Value;

        options.SchedulerScopeKey.Should().Be(JobSchedulerApplicationFixture.SchedulerScope);
        options.ProjectName.Should().Be("Test.Monica.JobScheduler");
        options.RecurringJobDebugMode.Should().BeTrue();
        options.TriggeredJobDebugMode.Should().BeTrue();

        scope.Resolve<IJobMetadataRepository>().Should().BeOfType<InMemoryJobMetadataRepository>();
        scope.Resolve<JobSchedulerDashboardFacade>().Should().NotBeNull();
        scope.Resolve<JobRegistry>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredKeyedService<IEventBus>(nameof(ModuleJobScheduler)).Should().NotBeNull();
        scope.ServiceProvider.GetRequiredKeyedService<ICancellationManager>(nameof(ModuleJobScheduler)).Should().NotBeNull();
    }
}

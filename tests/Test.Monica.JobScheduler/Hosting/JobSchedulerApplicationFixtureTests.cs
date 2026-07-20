using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.Modules;
using Monica.StateStore.Cancellation.Abstractions;
using Monica.Testing.Hosting;
using Xunit;

namespace Test.Monica.JobScheduler.Hosting;

public sealed class JobSchedulerApplicationFactory : MonicaTestApplicationFactory<ModuleJobScheduler>
{
    public const string SchedulerScope = "job-sociable-tests";
    private const string PROJECT_NAME = "Test.Monica.JobScheduler";

    protected override void ConfigureMonica(IMonicaBuilder builder)
    {
        builder.AddJobScheduler(options =>
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

public sealed class JobSchedulerApplicationFactoryTests(JobSchedulerApplicationFactory factory)
    : IClassFixture<JobSchedulerApplicationFactory>
{
    [Fact]
    public async Task CreateAsync_WhenModuleGraphBoots_ShouldResolveConfiguredJobSchedulerServices()
    {
        await using var application = await factory.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);
        await using var scope = application.CreateScope(TestContext.Current.CancellationToken);

        var options = scope.Resolve<IOptions<ModuleJobSchedulerOption>>().Value;

        options.SchedulerScopeKey.Should().Be(JobSchedulerApplicationFactory.SchedulerScope);
        options.ProjectName.Should().Be("Test.Monica.JobScheduler");
        options.RecurringJobDebugMode.Should().BeTrue();
        options.TriggeredJobDebugMode.Should().BeTrue();

        scope.Resolve<IJobMetadataRepository>().Should().BeOfType<InMemoryJobMetadataRepository>();
        scope.Resolve<JobSchedulerDashboardFacade>().Should().NotBeNull();
        scope.Resolve<JobRegistry>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredKeyedService<IEventBus>(nameof(ModuleJobScheduler)).Should().NotBeNull();
        scope.ServiceProvider.GetRequiredKeyedService<ICancellationManager>(nameof(ModuleJobScheduler)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenMultipleNamedHttpClientsAreConfigured_ShouldPreserveEveryScenarioClient()
    {
        using var primaryClient = new HttpClient();
        using var secondaryClient = new HttpClient();
        await using var application = await factory.CreateAsync(
            scenario => scenario
                .WithHttpClient("primary", primaryClient)
                .WithHttpClient("secondary", secondaryClient),
            TestContext.Current.CancellationToken);

        var clientFactory = application.Services.GetRequiredService<IHttpClientFactory>();

        clientFactory.CreateClient("primary").Should().BeSameAs(primaryClient);
        clientFactory.CreateClient("secondary").Should().BeSameAs(secondaryClient);
    }
}

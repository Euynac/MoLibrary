using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using Test.Monica.JobScheduler.Hosting;
using Xunit;

namespace Test.Monica.JobScheduler.Modules;

public class ModuleJobSchedulerCompositionTests
{
    [Fact]
    public void AddJobScheduler_WhenRequiredFeaturesAreMissing_ShouldReportAllFeatures()
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(typeof(ModuleJobScheduler).Assembly, typeof(ModuleServiceDiscovery).Assembly);
            });
            monica.AddJobScheduler();
        });

        var exception = act.Should().Throw<ModuleRegistrationException>().Which;
        exception.Message.Should().Contain("metadata-store");
        exception.Message.Should().Contain("provider");
        exception.Message.Should().Contain("scope");
        exception.Message.Should().Contain("service-discovery-state-store");
    }

    [Fact]
    public async Task AddJobScheduler_WhenAllRequiredFeaturesAreSelected_ShouldComposeProviderGraph()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(typeof(ModuleJobScheduler).Assembly, typeof(ModuleServiceDiscovery).Assembly);
            });
            monica.AddServiceDiscovery()
                .AsStandalone()
                .UseMemoryStorage();
            monica.AddJobScheduler(options => options.ProjectName = "Test.Project")
                .UseSchedulerScope("job-tests")
                .UseInMemoryProvider()
                .UseInMemoryMetadataRepository();
        });

        await using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var options = host.Services.GetRequiredService<IOptions<ModuleJobSchedulerOption>>().Value;

        application.Modules.IsRegistered(typeof(ModuleEventBus)).Should().BeTrue();
        application.Modules.IsRegistered(typeof(ModuleCancellationManager)).Should().BeTrue();
        application.Modules.IsRegistered(typeof(ModuleServiceDiscovery)).Should().BeTrue();
        application.Modules.IsRegistered(typeof(ModuleHostedService)).Should().BeTrue();
        application.Modules.IsRegistered(typeof(ModuleHealthCheck)).Should().BeTrue();
        options.SchedulerScopeKey.Should().Be("job-tests");
        host.Services.GetRequiredService<IOptions<ModuleServiceDiscoveryOption>>().Value.Role
            .Should().Be(ServiceDiscoveryRole.Standalone);
    }

    [Theory]
    [InlineData(ServiceDiscoveryRole.Worker)]
    [InlineData(ServiceDiscoveryRole.Registry)]
    [InlineData(ServiceDiscoveryRole.Standalone)]
    public async Task AddJobScheduler_ShouldComposeExactlyOneRoleOwnedPlane(ServiceDiscoveryRole role)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddServiceDiscovery(options => options.Role = role)
                .UseMemoryStorage();
            monica.AddJobScheduler()
                .UseSchedulerScope("role-tests")
                .UseInMemoryProvider()
                .UseInMemoryMetadataRepository();
        });

        await using var host = builder.Build();
        var hostedServiceTypes = host.Services.GetServices<IHostedService>()
            .Select(static service => service.GetType())
            .ToHashSet();

        hostedServiceTypes.Contains(typeof(JobDefinitionPublisherHostedService))
            .Should().Be(role == ServiceDiscoveryRole.Worker);
        hostedServiceTypes.Contains(typeof(JobWorkerManagerHostedService))
            .Should().Be(role is ServiceDiscoveryRole.Worker or ServiceDiscoveryRole.Standalone);
        hostedServiceTypes.Contains(typeof(JobDefinitionControlPlaneHostedService))
            .Should().Be(role is ServiceDiscoveryRole.Registry or ServiceDiscoveryRole.Standalone);
        hostedServiceTypes.Contains(typeof(JobSchedulerHostedService))
            .Should().Be(role is ServiceDiscoveryRole.Registry or ServiceDiscoveryRole.Standalone);
        hostedServiceTypes.Contains(typeof(JobConcurrencyGuardHostedService))
            .Should().Be(role is ServiceDiscoveryRole.Registry or ServiceDiscoveryRole.Standalone);
        hostedServiceTypes.Contains(typeof(LongIntervalSchedulerService))
            .Should().Be(role is ServiceDiscoveryRole.Registry or ServiceDiscoveryRole.Standalone);
        hostedServiceTypes.Contains(typeof(JobZombieDetectorHostedService))
            .Should().Be(role is ServiceDiscoveryRole.Registry or ServiceDiscoveryRole.Standalone);
        hostedServiceTypes.Contains(typeof(JobHistoryCleanupHostedService))
            .Should().Be(role is ServiceDiscoveryRole.Registry or ServiceDiscoveryRole.Standalone);

        if (role == ServiceDiscoveryRole.Worker)
        {
            host.Services.GetService<JobDefinitionReconciler>().Should().BeNull();
            host.Services.GetService<JobSchedulerFacade>().Should().BeNull();
            host.Services.GetService<JobSchedulerDashboardFacade>().Should().BeNull();
            host.Services.GetService<JobSchedulerMonitorFacade>().Should().BeNull();
            host.Services.GetService<JobSchedulerAnalyticsFacade>().Should().BeNull();
            host.Services.GetService<JobSchedulerQueryFacade>().Should().BeNull();
            host.Services.GetService<JobDispatcher>().Should().BeNull();
            host.Services.GetService<RecurringJobScheduler>().Should().BeNull();
            host.Services.GetService<TriggeredJobScheduler>().Should().BeNull();
            host.Services.GetService<JobHistoryCleanupExecutor>().Should().BeNull();
        }
        else if (role == ServiceDiscoveryRole.Registry)
        {
            host.Services.GetRequiredService<JobDefinitionReconciler>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerDashboardFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerMonitorFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerAnalyticsFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerQueryFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobDispatcher>().Should().NotBeNull();
            host.Services.GetRequiredService<RecurringJobScheduler>().Should().NotBeNull();
            host.Services.GetRequiredService<TriggeredJobScheduler>().Should().NotBeNull();
            host.Services.GetRequiredService<JobHistoryCleanupExecutor>().Should().NotBeNull();
            host.Services.GetService<JobExecutor>().Should().BeNull();
            host.Services.GetService<JobRegistry>().Should().BeNull();
            host.Services.GetService<JobOrchestrator>().Should().BeNull();
            host.Services.GetService<ITriggeredJobManager>().Should().BeNull();
        }
        else
        {
            host.Services.GetRequiredService<JobDefinitionReconciler>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerDashboardFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerMonitorFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerAnalyticsFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobSchedulerQueryFacade>().Should().NotBeNull();
            host.Services.GetRequiredService<JobDispatcher>().Should().NotBeNull();
            host.Services.GetRequiredService<RecurringJobScheduler>().Should().NotBeNull();
            host.Services.GetRequiredService<TriggeredJobScheduler>().Should().NotBeNull();
            host.Services.GetRequiredService<JobHistoryCleanupExecutor>().Should().NotBeNull();
        }

        host.Services.GetRequiredService<IOptions<ModuleServiceDiscoveryOption>>().Value.Role
            .Should().Be(role);
    }

    [Fact]
    public void AddJobScheduler_WhenRegistryDiscoversExecutableJobs_ShouldRejectMixedPlaneComposition()
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(typeof(WorkerExecutionProbeJob).Assembly);
            });
            monica.AddServiceDiscovery()
                .AsRegistry()
                .UseMemoryStorage();
            monica.AddJobScheduler()
                .UseSchedulerScope("registry-with-jobs")
                .UseInMemoryProvider()
                .UseInMemoryMetadataRepository();
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Registry*cannot own executable job types*Worker*Standalone*");
    }
}

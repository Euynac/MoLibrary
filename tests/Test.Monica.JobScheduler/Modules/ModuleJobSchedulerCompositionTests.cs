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
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.Modules;

public sealed class ModuleJobSchedulerCompositionTests
{
    [Fact]
    public void AddJobScheduler_WhenRequiredFeaturesAreMissing_ShouldReportAllFeatures()
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJobScheduler();
        });

        var exception = act.Should().Throw<ModuleRegistrationException>().Which;
        exception.Message.Should().Contain("scheduler-store");
        exception.Message.Should().Contain("scheduler-scope");
        exception.Message.Should().Contain("catalog-release");
    }

    [Fact]
    public void ValidateOptions_WhenLocalWorkerRevisionDiffersFromManifest_ShouldFailFast()
    {
        var options = new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.Worker,
            ProjectName = "owner-a",
            SchedulerScopeKey = "worker-tests",
            CatalogReleaseId = "release-1",
            DeploymentGeneration = 1,
            LocalOwnerId = "owner-a",
            LocalWorkerRevisionId = "worker-r2"
        };
        options.CatalogOwners.Add(new JobCatalogOwnerManifest("owner-a", "worker-r1"));

        var act = () => new ModuleJobScheduler().ValidateOptions(options, profileName: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*worker-r2*does not match manifest revision*worker-r1*owner-a*");
    }

    [Theory]
    [InlineData(JobSchedulerRole.ControlPlane)]
    [InlineData(JobSchedulerRole.Worker)]
    [InlineData(JobSchedulerRole.Standalone)]
    public async Task AddJobScheduler_ShouldComposeOnlyTheSelectedRuntimePlanes(JobSchedulerRole role)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(typeof(ModuleJobScheduler).Assembly);
            });
            var scheduler = monica.AddJobScheduler(options => options.ProjectName = "owner-a")
                .UseInMemoryStore()
                .UseSchedulerScope("role-tests")
                .UseCatalogRelease(
                    "release-1",
                    1,
                    [new JobCatalogOwnerManifest("owner-a", "worker-r1")])
                .UseLocalWorkerIdentity("owner-a", "worker-r1");
            switch (role)
            {
                case JobSchedulerRole.ControlPlane:
                    scheduler.AsControlPlane();
                    break;
                case JobSchedulerRole.Worker:
                    scheduler.AsWorker();
                    break;
                case JobSchedulerRole.Standalone:
                    scheduler.AsStandalone();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }
        });

        await using var host = builder.Build();
        var hostedServiceTypes = host.Services.GetServices<IHostedService>()
            .Select(static service => service.GetType())
            .ToHashSet();
        var runsControlPlane = role is JobSchedulerRole.ControlPlane or JobSchedulerRole.Standalone;
        var runsWorker = role is JobSchedulerRole.Worker or JobSchedulerRole.Standalone;

        hostedServiceTypes.Contains(typeof(JobControlPlaneHostedService)).Should().Be(runsControlPlane);
        hostedServiceTypes.Contains(typeof(JobWorkerHostedService)).Should().Be(runsWorker);
        (host.Services.GetService<JobSchedulerFacade>() is not null).Should().Be(runsControlPlane);
        (host.Services.GetService<JobRegistry>() is not null).Should().Be(runsWorker);
        (host.Services.GetService<JobOrchestrator>() is not null).Should().Be(runsWorker);
        (host.Services.GetService<ITriggeredJobManager>() is not null).Should().Be(runsWorker);
        host.Services.GetRequiredService<IJobSchedulerStore>().Should().NotBeNull();
        host.Services.GetRequiredService<IOptions<ModuleJobSchedulerOption>>().Value.Role.Should().Be(role);
    }

    [Fact]
    public async Task AddJobScheduler_ShouldNotRegisterServiceDiscoveryAsACorrectnessDependency()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJobScheduler()
                .AsControlPlane()
                .UseInMemoryStore()
                .UseSchedulerScope("control-tests")
                .UseCatalogRelease(
                    "release-1",
                    1,
                    [new JobCatalogOwnerManifest("owner-a", "worker-r1")]);
        });

        await using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        application.Modules.IsRegistered(typeof(ModuleServiceDiscovery)).Should().BeFalse();
    }

    [Fact]
    public async Task AddJobScheduler_WhenControlPlaneManifestIsEmpty_ShouldActivateAnEmptyCatalog()
    {
        const string scope = "empty-control-plane";
        const string release = "empty-release";
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJobScheduler()
                .AsControlPlane()
                .UseInMemoryStore()
                .UseSchedulerScope(scope)
                .UseCatalogRelease(release, 1, []);
        });

        await using var host = builder.Build();
        var store = host.Services.GetRequiredService<IJobSchedulerStore>();
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(new JobCatalogReleaseManifest(scope, release, []), 1),
            TestContext.Current.CancellationToken);
        var activation = await store.TryActivateReleaseAsync(
            scope,
            release,
            TestContext.Current.CancellationToken);
        var catalog = await store.GetActiveCatalogAsync(scope, TestContext.Current.CancellationToken);

        activation.Status.Should().Be(JobCatalogActivationStatus.Activated);
        catalog.Should().NotBeNull();
        catalog!.Definitions.Should().BeEmpty();
    }
}

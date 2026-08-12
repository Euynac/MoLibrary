using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
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
    [InlineData(ServiceDiscoveryRole.Worker, false)]
    [InlineData(ServiceDiscoveryRole.Registry, true)]
    [InlineData(ServiceDiscoveryRole.Standalone, true)]
    public async Task AddJobScheduler_ShouldDeriveControlPlaneFromServiceDiscoveryRole(
        ServiceDiscoveryRole role,
        bool expectControlPlane)
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
        host.Services.GetServices<IHostedService>().OfType<JobSchedulerHostedService>().Any()
            .Should().Be(expectControlPlane);
        host.Services.GetRequiredService<IOptions<ModuleServiceDiscoveryOption>>().Value.Role
            .Should().Be(role);
    }
}

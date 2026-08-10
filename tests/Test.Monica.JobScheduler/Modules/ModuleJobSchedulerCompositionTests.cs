using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
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

        act.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*metadata-store*provider*scope*");
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
        options.RunControlPlane.Should().BeTrue();
    }
}

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.Modules;

public class ModuleJobSchedulerGuideTests
{
    [Fact]
    public void Register_WhenRequiredMethodsAreMissing_ShouldReportAllMissingConfigurationKeys()
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
            .WithMessage("*CONFIG_PROVIDER*CONFIG_METADATA_STORE*CONFIG_SCOPE*");
    }

    [Fact]
    public async Task Register_WhenAllRequiredMethodsAreConfigured_ShouldRecordRequestsAndDependencies()
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

        var dependencies = application.Dependencies.CalculateModuleDependencies(BuiltInModuleKey.JobScheduler);
        dependencies.Should().Contain(BuiltInModuleKey.EventBus);
        dependencies.Should().Contain(BuiltInModuleKey.CancellationManager);
        dependencies.Should().Contain(BuiltInModuleKey.ServiceDiscovery);
        dependencies.Should().Contain(BuiltInModuleKey.HostedService);
    }
}

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Modules;

public class ModuleJobSchedulerUITests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterUiSupportHelpersWithExpectedLifetimes()
    {
        var services = new ServiceCollection();
        var module = new ModuleJobSchedulerUI(new ModuleJobSchedulerUIOption());

        module.ConfigureServices(services);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(JobStateColorResolver) &&
            descriptor.ImplementationType == typeof(JobStateColorResolver) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(JobArgsJsonSchemaSupport) &&
            descriptor.ImplementationType == typeof(JobArgsJsonSchemaSupport) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(CronExpressionSupport) &&
            descriptor.ImplementationType == typeof(CronExpressionSupport) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task ClaimDependencies_WhenPagesAreEnabled_ShouldIncludeUiDependencies()
    {
        await using var host = ComposeJobSchedulerUI(disablePages: false);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var dependencies = application.Dependencies.CalculateModuleDependencies(BuiltInModuleKey.JobSchedulerUI);
        dependencies.Should().Contain(BuiltInModuleKey.Localization);
        dependencies.Should().Contain(BuiltInModuleKey.JobScheduler);
        dependencies.Should().Contain(BuiltInModuleKey.UIStackTrace);
        dependencies.Should().Contain(BuiltInModuleKey.UICore);
    }

    [Fact]
    public async Task ClaimDependencies_WhenPagesAreDisabled_ShouldSkipUiCoreDependency()
    {
        await using var host = ComposeJobSchedulerUI(disablePages: true);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var dependencies = application.Dependencies.CalculateModuleDependencies(BuiltInModuleKey.JobSchedulerUI);

        dependencies.Should().Contain(BuiltInModuleKey.Localization);
        dependencies.Should().Contain(BuiltInModuleKey.JobScheduler);
        dependencies.Should().Contain(BuiltInModuleKey.UIStackTrace);
        dependencies.Should().NotContain(BuiltInModuleKey.UICore);
    }

    private static WebApplication ComposeJobSchedulerUI(bool disablePages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(
                    typeof(ModuleJobSchedulerUI).Assembly,
                    typeof(ModuleJobScheduler).Assembly,
                    typeof(ModuleShellUI).Assembly);
            });
            monica.AddJobScheduler()
                .UseSchedulerScope("job-ui-tests")
                .UseInMemoryProvider()
                .UseInMemoryMetadataRepository();
            monica.AddJobSchedulerUI(options => options.DisableJobSchedulerPages = disablePages);
        });

        return builder.Build();
    }
}

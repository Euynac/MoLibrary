using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Modules;

public class ModuleJobSchedulerUITests
{
    [Fact]
    public async Task Composition_ShouldRegisterUiSupportHelpersWithExpectedLifetimes()
    {
        await using var host = ComposeJobSchedulerUI(useConvenienceEntry: false);
        using var firstScope = host.Services.CreateScope();
        using var secondScope = host.Services.CreateScope();

        firstScope.ServiceProvider.GetRequiredService<JobStateColorResolver>().Should()
            .NotBeSameAs(secondScope.ServiceProvider.GetRequiredService<JobStateColorResolver>());
        firstScope.ServiceProvider.GetRequiredService<JobArgsJsonSchemaSupport>().Should()
            .BeSameAs(secondScope.ServiceProvider.GetRequiredService<JobArgsJsonSchemaSupport>());
        firstScope.ServiceProvider.GetRequiredService<CronExpressionSupport>().Should()
            .BeSameAs(secondScope.ServiceProvider.GetRequiredService<CronExpressionSupport>());
    }

    [Fact]
    public async Task AddJobSchedulerUI_ShouldComposeRuntimeAndUiDependencies()
    {
        await using var host = ComposeJobSchedulerUI(useConvenienceEntry: true);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var dependencies = GetDirectDependencyTypes(application, typeof(ModuleJobSchedulerUI));

        dependencies.Should().Contain(typeof(ModuleLocalization));
        dependencies.Should().Contain(typeof(ModuleJobScheduler));
        dependencies.Should().Contain(typeof(ModuleStackTraceUI));
        dependencies.Should().Contain(typeof(ModuleShellUI));
    }

    [Fact]
    public async Task DirectAndTransitiveComposition_ShouldDeclareEquivalentIntrinsicDependencies()
    {
        await using var directHost = ComposeJobSchedulerUI(useConvenienceEntry: true);
        await using var transitiveHost = ComposeJobSchedulerUI(useConvenienceEntry: false);
        var directDependencies = GetDirectDependencyTypes(
            directHost.Services.GetRequiredService<MonicaApplication>(),
            typeof(ModuleJobSchedulerUI));
        var transitiveDependencies = GetDirectDependencyTypes(
            transitiveHost.Services.GetRequiredService<MonicaApplication>(),
            typeof(ModuleJobSchedulerUI));

        transitiveDependencies.Should().BeEquivalentTo(directDependencies);
        transitiveDependencies.Should().Contain(typeof(ModuleJobScheduler));
        transitiveDependencies.Should().Contain(typeof(ModuleLocalization));
        transitiveDependencies.Should().Contain(typeof(ModuleStackTraceUI));
        transitiveDependencies.Should().Contain(typeof(ModuleShellUI));
    }

    private static WebApplication ComposeJobSchedulerUI(bool useConvenienceEntry)
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
            monica.AddServiceDiscovery()
                .AsStandalone()
                .UseMemoryStorage();
            monica.AddJobScheduler()
                .UseSchedulerScope("job-ui-tests")
                .UseInMemoryProvider()
                .UseInMemoryMetadataRepository();
            if (useConvenienceEntry)
            {
                monica.AddJobSchedulerUI();
            }
            else
            {
                monica.AddModule<JobSchedulerUIConsumerModule, JobSchedulerUIConsumerModuleOption>();
            }
        });

        return builder.Build();
    }

    private static IReadOnlySet<Type> GetDirectDependencyTypes(
        MonicaApplication application,
        Type moduleType)
    {
        var moduleKey = application.Dependencies.ModuleKeysByType[moduleType];
        return application.Dependencies.DependenciesByModule[moduleKey]
            .Select(dependency => application.Dependencies.ModuleTypesByKey[dependency])
            .ToHashSet();
    }
}

public sealed class JobSchedulerUIConsumerModule : MonicaModule<JobSchedulerUIConsumerModuleOption>
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption>();
    }
}

public sealed class JobSchedulerUIConsumerModuleOption : ModuleOptions<JobSchedulerUIConsumerModule>;

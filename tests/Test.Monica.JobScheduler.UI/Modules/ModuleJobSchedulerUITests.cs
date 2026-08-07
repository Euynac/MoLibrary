using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
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
        await using var host = ComposeJobSchedulerUI(includePageFeature: false);
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
        await using var host = ComposeJobSchedulerUI(includePageFeature: true);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var dependencies = GetDirectDependencyTypes(application, typeof(ModuleJobSchedulerUI));

        dependencies.Should().Contain(typeof(ModuleLocalization));
        dependencies.Should().Contain(typeof(ModuleJobScheduler));
        dependencies.Should().Contain(typeof(ModuleStackTraceUI));
        dependencies.Should().Contain(typeof(ModuleShellUI));
    }

    [Fact]
    public async Task AddModule_WithoutPageFeature_ShouldKeepOnlyIntrinsicDependencies()
    {
        await using var host = ComposeJobSchedulerUI(includePageFeature: false);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var dependencies = GetDirectDependencyTypes(application, typeof(ModuleJobSchedulerUI));

        dependencies.Should().BeEquivalentTo([
            typeof(ModuleJobScheduler),
            typeof(ModuleShellUI)
        ]);
    }

    private static WebApplication ComposeJobSchedulerUI(bool includePageFeature)
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
            if (includePageFeature)
            {
                monica.AddJobSchedulerUI();
            }
            else
            {
                monica.AddModule<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption>();
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

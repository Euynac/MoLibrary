using AwesomeAssertions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;
using Monica.Modules;
using Test.Monica.Modularity;
using Xunit;

namespace Test.Monica.JobScheduler.Modules;

public class ModuleJobSchedulerGuideTests
{
    [Fact]
    public void Register_WhenRequiredMethodsAreMissing_ShouldReportAllMissingConfigurationKeys()
    {
        using var scope = ModuleTestScope.Create(
            typeof(ModuleJobScheduler).Assembly,
            typeof(ModuleServiceDiscovery).Assembly);

        new ModuleJobSchedulerGuide().Register();

        ModuleRegistry.TryGetModuleRequestInfo(typeof(ModuleJobScheduler), out var registerState).Should().BeTrue();
        registerState.Should().NotBeNull();
        registerState!.GetMissingRequiredConfigMethodKeys().Should().BeEquivalentTo(
            "CONFIG_PROVIDER",
            "CONFIG_METADATA_STORE",
            "CONFIG_SCOPE");
    }

    [Fact]
    public void Register_WhenAllRequiredMethodsAreConfigured_ShouldRecordRequestsAndDependencies()
    {
        using var scope = ModuleTestScope.Create(
            typeof(ModuleJobScheduler).Assembly,
            typeof(ModuleServiceDiscovery).Assembly);

        new ModuleJobSchedulerGuide()
            .Register()
            .UseSchedulerScope("job-tests")
            .UseInMemoryProvider()
            .UseInMemoryMetadataRepository();

        new ModuleJobScheduler(new ModuleJobSchedulerOption
        {
            SchedulerScopeKey = "job-tests",
            ProjectName = "Test.Project"
        }).ClaimDependencies();

        ModuleRegistry.TryGetModuleRequestInfo(typeof(ModuleJobScheduler), out var registerState).Should().BeTrue();
        registerState.Should().NotBeNull();
        registerState!.GetMissingRequiredConfigMethodKeys().Should().BeEmpty();

        var dependencies = ModuleDependencyAnalyzer.CalculateModuleDependencies(BuiltInModuleKey.JobScheduler);
        dependencies.Should().Contain(BuiltInModuleKey.EventBus);
        dependencies.Should().Contain(BuiltInModuleKey.CancellationManager);
        dependencies.Should().Contain(BuiltInModuleKey.ServiceDiscovery);
        dependencies.Should().Contain(BuiltInModuleKey.HostedService);
    }
}

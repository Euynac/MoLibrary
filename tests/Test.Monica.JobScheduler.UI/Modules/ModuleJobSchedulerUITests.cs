using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services.Support;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using Monica.Modules;
using Test.Monica.Modularity;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Modules;

public class ModuleJobSchedulerUITests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterUiSupportHelpersAsSingletons()
    {
        var services = new ServiceCollection();
        var module = new ModuleJobSchedulerUI(new ModuleJobSchedulerUIOption());

        module.ConfigureServices(services);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(JobStateColorResolver) &&
            descriptor.ImplementationType == typeof(JobStateColorResolver) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
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
    public void ClaimDependencies_WhenPagesAreEnabled_ShouldIncludeUiDependencies()
    {
        using var scope = ModuleTestScope.Create(
            typeof(ModuleJobSchedulerUI).Assembly,
            typeof(ModuleJobScheduler).Assembly,
            typeof(ModuleShellUI).Assembly);

        var module = new ModuleJobSchedulerUI(new ModuleJobSchedulerUIOption());

        module.ClaimDependencies();

        var dependencies = ModuleDependencyAnalyzer.CalculateModuleDependencies(BuiltInModuleKey.JobSchedulerUI);
        dependencies.Should().Contain(BuiltInModuleKey.Localization);
        dependencies.Should().Contain(BuiltInModuleKey.JobScheduler);
        dependencies.Should().Contain(BuiltInModuleKey.UIStackTrace);
        dependencies.Should().Contain(BuiltInModuleKey.UICore);
    }

    [Fact]
    public void ClaimDependencies_WhenPagesAreDisabled_ShouldSkipUiCoreDependency()
    {
        using var scope = ModuleTestScope.Create(
            typeof(ModuleJobSchedulerUI).Assembly,
            typeof(ModuleJobScheduler).Assembly,
            typeof(ModuleShellUI).Assembly);

        var module = new ModuleJobSchedulerUI(new ModuleJobSchedulerUIOption
        {
            DisableJobSchedulerPages = true
        });

        module.ClaimDependencies();

        var dependencies = ModuleDependencyAnalyzer.CalculateModuleDependencies(BuiltInModuleKey.JobSchedulerUI);
        dependencies.Should().Contain(BuiltInModuleKey.Localization);
        dependencies.Should().Contain(BuiltInModuleKey.JobScheduler);
        dependencies.Should().Contain(BuiltInModuleKey.UIStackTrace);
        dependencies.Should().NotContain(BuiltInModuleKey.UICore);
    }
}

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.UIExecutionPipeline.State;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Framework.UI.ExecutionPipeline;

public sealed class ModuleExecutionPipelineUITests
{
    [Fact]
    public void Catalog_ui_is_a_non_web_module()
    {
        var module = new ModuleExecutionPipelineUI(new ModuleExecutionPipelineUIOption());

        module.Should().BeAssignableTo<IModule>();
        module.Should().NotBeAssignableTo<IWebModule>();
    }

    [Fact]
    public void Configure_services_registers_scoped_page_state()
    {
        var services = new ServiceCollection();
        var module = new ModuleExecutionPipelineUI(new ModuleExecutionPipelineUIOption());

        module.ConfigureServices(services);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ExecutionPipelinePageState) &&
            descriptor.ImplementationType == typeof(ExecutionPipelinePageState) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Configure_services_skips_page_state_when_page_is_disabled()
    {
        var services = new ServiceCollection();
        var module = new ModuleExecutionPipelineUI(new ModuleExecutionPipelineUIOption
        {
            DisablePage = true
        });

        module.ConfigureServices(services);

        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(ExecutionPipelinePageState));
    }

    [Fact]
    public async Task Enabled_page_claims_pipeline_localization_and_shell_dependencies()
    {
        await using var app = Compose(disablePage: false);
        var application = app.Services.GetRequiredService<MonicaApplication>();
        var dependencies = application.Dependencies.CalculateModuleDependencies(BuiltInModuleKey.ExecutionPipelineUI);

        dependencies.Should().Contain(BuiltInModuleKey.ExecutionPipeline);
        dependencies.Should().Contain(BuiltInModuleKey.Localization);
        dependencies.Should().Contain(BuiltInModuleKey.UICore);
    }

    [Fact]
    public async Task Disabled_page_skips_all_catalog_page_dependencies()
    {
        await using var app = Compose(disablePage: true);
        var application = app.Services.GetRequiredService<MonicaApplication>();
        var dependencies = application.Dependencies.CalculateModuleDependencies(BuiltInModuleKey.ExecutionPipelineUI);

        dependencies.Should().NotContain(BuiltInModuleKey.ExecutionPipeline);
        dependencies.Should().NotContain(BuiltInModuleKey.Localization);
        dependencies.Should().NotContain(BuiltInModuleKey.UICore);
    }

    private static WebApplication Compose(bool disablePage)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(
                    typeof(ModuleExecutionPipelineUI).Assembly,
                    typeof(ModuleExecutionPipeline).Assembly,
                    typeof(ModuleShellUI).Assembly);
            });
            monica.AddExecutionPipelineUI(options => options.DisablePage = disablePage);
        });

        return builder.Build();
    }
}

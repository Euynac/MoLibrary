using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Models;
using Monica.UI.Shell.Support;
using Xunit;

namespace Test.Monica.UI.UIModuleSystem;

public sealed class ModuleSystemUIModuleTests
{
    [Fact]
    public void Options_WhenNoPagePolicyIsConfigured_ShouldInheritShellPolicy()
    {
        var options = new ModuleSystemUIOption();

        options.AuthorizationPolicyOverride.Should().BeNull();
    }

    [Fact]
    public async Task AddModuleSystemUI_ShouldPublishProtectedPageUsingSharedAccessPolicy()
    {
        await using var app = Compose(configurePipeline: true);
        var catalog = app.Services.GetRequiredService<IPageCatalog>();
        var route = ModuleSystemPage.MODULE_SYSTEM_DASHBOARD_URL.Trim('/');
        var page = catalog.GetRegisteredPages().Single(definition => definition.Route == route);

        page.ComponentType.Should().Be(typeof(ModuleSystemPage));
        page.DisplayName.ResourceType.Should().Be(typeof(ModuleSystemResource));
        page.AccessPolicyType.Should().Be(typeof(OperationalPageAccessPolicy<ModuleSystemUIOption>));
    }

    [Fact]
    public async Task Composition_ShouldRegisterSharedAccessPolicyAsScoped()
    {
        await using var app = Compose();
        using var firstScope = app.Services.CreateScope();
        using var secondScope = app.Services.CreateScope();

        var firstAccess = firstScope.ServiceProvider
            .GetRequiredService<OperationalPageAccessPolicy<ModuleSystemUIOption>>();

        firstScope.ServiceProvider.GetRequiredService<OperationalPageAccessPolicy<ModuleSystemUIOption>>()
            .Should().BeSameAs(firstAccess);
        secondScope.ServiceProvider.GetRequiredService<OperationalPageAccessPolicy<ModuleSystemUIOption>>()
            .Should().NotBeSameAs(firstAccess);
    }

    private static WebApplication Compose(bool configurePipeline = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ModuleSystemUI).Assembly.GetName().Name
        });
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(typeof(ModuleSystemUI).Assembly);
            });
            monica.AddModuleSystemUI();
        });

        var app = builder.Build();
        if (configurePipeline)
        {
            app.UseMonica();
            app.MapMonica();
        }

        return app;
    }
}

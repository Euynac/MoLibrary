using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Dapr.Modules;

public sealed class ModuleDaprClientCompositionTests
{
    [Fact]
    public void Build_WhenUsingGenericHost_ShouldRetainDaprClientServicesWithoutRequiringWebHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddDaprClient();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        Assert.Contains(
            application.Modules.RuntimeSnapshots,
            snapshot => snapshot.ModuleType == typeof(ModuleDaprClient)
                        && snapshot.IsWebModule
                        && !snapshot.RequiresWebHost);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapMonica_WhenUsingWebHost_ShouldHonorMetadataEndpointSwitch(bool enableMinimalApi)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddDaprClient(options => options.EnableMinimalApi = enableMinimalApi);
        });

        await using var application = builder.Build();
        application.UseMonica();
        application.MapMonica();

        var routes = ((IEndpointRouteBuilder)application).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        if (enableMinimalApi)
        {
            Assert.Contains("/dapr/metadata", routes);
        }
        else
        {
            Assert.DoesNotContain("/dapr/metadata", routes);
        }
    }

    [Fact]
    public void Build_WhenDaprEventBusIsSelected_ShouldIncludeDaprClientTransitively()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddEventBus(options => options.DisableAutoDiscovery = true)
                .UseDaprProvider();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        Assert.True(application.Modules.IsRegistered(typeof(ModuleDaprEventBus)));
        Assert.True(application.Modules.IsRegistered(typeof(ModuleDaprClient)));
    }
}

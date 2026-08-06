using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers and configures the Dapr module.
        /// </summary>
        public ModuleRegistration<ModuleDapr, ModuleDaprOption> AddDapr(Action<ModuleDaprOption>? action = null)
        {
            return builder.AddModule<ModuleDapr, ModuleDaprOption>(action);
        }
    }
}

public class ModuleDapr : MonicaModule<ModuleDaprOption>, IWebModule
{

    public override void ConfigureServices(ModuleContext<ModuleDaprOption> context)
    {
        var services = context.Services;
        // Disabled temporarily until https://github.com/dapr/dotnet-sdk/issues/779 is resolved.
        //builder.Configuration.AddDaprSecretStore(
        //    "secretstore",
        //    new DaprClientBuilder().Build());
    }

    public override void ConfigureEndpoints(WebModuleContext<ModuleDaprOption> context)
    {
        var app = context.ApplicationBuilder;
        UseEndpoints(context, endpoints =>
        {
            var tagName = Option.GetApiGroupName();
            endpoints.MapGet("/dapr/metadata", async (HttpResponse response, HttpContext context) =>
            {
                var daprClient = context.RequestServices.GetRequiredService<DaprClient>();
                var res = await daprClient.GetMetadataAsync();
                await context.Response.WriteAsJsonAsync(res);
            })
            .WithName("Get Dapr sidecar metadata")
            .WithTags(tagName)
            .WithSummary("Gets metadata reported by the Dapr sidecar.")
            .WithDescription("Returns metadata reported by the Dapr sidecar.");
        });
    }
}



public class ModuleDaprOption : MinimalApiModuleOptions<ModuleDapr>
{
    
}

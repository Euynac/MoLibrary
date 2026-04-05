using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers and configures the Dapr module.
        /// </summary>
        public static ModuleDaprGuide AddDapr(Action<ModuleDaprOption>? action = null)
        {
            return new ModuleDaprGuide().Register(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Dapr)]
public class ModuleDapr(ModuleDaprOption option) : ModuleBase<ModuleDapr, ModuleDaprOption, ModuleDaprGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // Disabled temporarily until https://github.com/dapr/dotnet-sdk/issues/779 is resolved.
        //builder.Configuration.AddDaprSecretStore(
        //    "secretstore",
        //    new DaprClientBuilder().Build());
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();
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

public class ModuleDaprGuide : ModuleGuide<ModuleDapr, ModuleDaprOption, ModuleDaprGuide>
{

}

public class ModuleDaprOption : MinimalApiModuleOptions<ModuleDapr>
{
    
}

using System.Text.RegularExpressions;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Dapr 模块
        /// </summary>
        public static ModuleDaprGuide AddDapr(Action<ModuleDaprOption>? action = null)
        {
            return new ModuleDaprGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Dapr)]
public partial class ModuleDapr(ModuleDaprOption option) : MoModule<ModuleDapr, ModuleDaprOption, ModuleDaprGuide>(option)
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
            .WithName("获取Dapr边车元数据")
            .WithTags(tagName)
            .WithSummary("获取Dapr边车元数据")
            .WithDescription("获取Dapr边车元数据");
        });
    }

   
    [GeneratedRegex(@"/v1\.0/invoke/(.+)/method/(.+)")]
    private static partial Regex DaprInvocationRegex();
}

public class ModuleDaprGuide : MoModuleGuide<ModuleDapr, ModuleDaprOption, ModuleDaprGuide>
{

}

public class ModuleDaprOption : MoModuleOptionWithMinimalApi<ModuleDapr>
{
    
}

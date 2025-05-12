using System.Net;
using System.Text.RegularExpressions;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprBuilderExtensions
{
    public static ModuleDaprGuide AddMoModuleDapr(this IServiceCollection services,
        Action<ModuleDaprOption>? action = null)
    {
        return new ModuleDaprGuide().Register(action);
    }
}

public partial class ModuleDapr(ModuleDaprOption option) : MoModule<ModuleDapr, ModuleDaprOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Dapr;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        // Disabled temporarily until https://github.com/dapr/dotnet-sdk/issues/779 is resolved.
        //builder.Configuration.AddDaprSecretStore(
        //    "secretstore",
        //    new DaprClientBuilder().Build());
        return Res.Ok();
    }

    public override Res ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetSwaggerGroupName(), Description = "Dapr相关接口" }
            };
            endpoints.Map("/dapr/invocation/{*rest}", async (string rest, HttpResponse response, HttpContext context) =>
            {
                var daprClient = context.RequestServices.GetRequiredService<DaprClient>();
                var req = context.Request;
                var regex = DaprInvocationRegex();
                var match = regex.Match(req.GetDisplayUrl());
                if (match.Success)
                {
                    var appid = match.Groups[1].Value;
                    var method = match.Groups[2].Value;
                    var data = await req.Body.ReadAsAsStringWithoutChangePosAsync();
                    var reqToDapr = daprClient.CreateInvokeMethodRequest(HttpMethod.Parse(req.Method), appid, method, [], data);
                    var res = await daprClient.InvokeMethodWithResponseAsync(reqToDapr);
                    if (!res.IsSuccessStatusCode && res.StatusCode is not HttpStatusCode.BadRequest and HttpStatusCode.InternalServerError)
                    {
                        await context.Response.WriteAsJsonAsync(Res.CreateError(res, res.ToString()));
                        return;
                    }
                    response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(await res.Content.ReadAsStringAsync());
                    return;
                }

                await context.Response.WriteAsJsonAsync(Res.Fail("调用方式错误"));

            }).WithName("Dapr服务间调用").WithOpenApi(operation =>
            {
                operation.Summary = "Dapr服务间调用";
                operation.Description = "Dapr服务间调用(反向代理)";
                operation.Tags = tagGroup;
                return operation;
            });
            endpoints.MapGet("/dapr/metadata", async (HttpResponse response, HttpContext context) =>
            {
                var daprClient = context.RequestServices.GetRequiredService<DaprClient>();
                var res = await daprClient.GetMetadataAsync();
                await context.Response.WriteAsJsonAsync(res);
            }).WithName("获取Dapr边车元数据").WithOpenApi(operation =>
            {
                operation.Summary = "获取Dapr边车元数据";
                operation.Description = "获取Dapr边车元数据";
                operation.Tags = tagGroup;
                return operation;
            });


        });
        return base.ConfigureEndpoints(app);
    }

   
    [GeneratedRegex(@"/v1\.0/invoke/(.+)/method/(.+)")]
    private static partial Regex DaprInvocationRegex();
}

public class ModuleDaprGuide : MoModuleGuide<ModuleDapr, ModuleDaprOption, ModuleDaprGuide>
{

}

public class ModuleDaprOption : MoModuleControllerOption<ModuleDapr>
{
    
}

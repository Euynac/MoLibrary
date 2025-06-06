using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Modules;

public class ModuleRegisterCentre(ModuleRegisterCentreOption option) : MoModule<ModuleRegisterCentre, ModuleRegisterCentreOption, ModuleRegisterCentreGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.RegisterCentre;
    }

    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        if (option.ThisIsCentreClient)
        {
            var connector = app.ApplicationServices.GetService<IRegisterCentreServerConnector>();
            if (connector == null) throw new InvalidOperationException($"无法解析{nameof(IRegisterCentreServerConnector)}，可能未注册{nameof(IRegisterCentreServerConnector)}及{nameof(IRegisterCentreClient)}实现");
            Task.Factory.StartNew(async () =>
            {
                await connector.DoingRegister();
            }, TaskCreationOptions.LongRunning);
        }
        
        return base.ConfigureApplicationBuilder(app);
    }
    
    public override Res ConfigureEndpoints(IApplicationBuilder app)
    {
        if (option.ThisIsCentreServer)
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = option.GetSwaggerGroupName(), Description = "注册中心" } };
                endpoints.MapPost(MoRegisterCentreConventions.ServerCentreRegister, async (RegisterServiceStatus req, [FromServices] IRegisterCentreServer centre) =>
                {
                    if ((await centre.Register(req)).IsFailed(out var error)) return error;
                    return Res.Ok("注册成功");
                }).WithName("微服务注册热配置中心").WithOpenApi(operation =>
                {
                    operation.Summary = "微服务注册配置中心";
                    operation.Description = "微服务注册配置中心";
                    operation.Tags = tagGroup;
                    return operation;
                });

                endpoints.MapGet(MoRegisterCentreConventions.ServerCentreGetServicesStatus, async ([FromServices] IRegisterCentreServer centre) =>
                {
                    if ((await centre.GetServicesStatus()).IsFailed(out var error, out var data))
                        return error.GetResponse();
                    return Res.Create(data, ResponseCode.Ok).GetResponse();
                }).WithName("获取所有微服务状态").WithOpenApi(operation =>
                {
                    operation.Summary = "获取所有微服务状态";
                    operation.Description = "获取所有微服务状态";
                    operation.Tags = tagGroup;
                    return operation;
                });


                endpoints.MapGet(MoRegisterCentreConventions.ServerCentreUnregisterAll, async ([FromServices] IRegisterCentreServer centre) =>
                {
                    var res = await centre.UnregisterAll();
                    return res.GetResponse();
                }).WithName("清空所有注册").WithOpenApi(operation =>
                {
                    operation.Summary = "清空所有注册";
                    operation.Description = "清空所有注册";
                    operation.Tags = tagGroup;
                    return operation;
                });

            });
        }
        else if (option.ThisIsCentreClient)
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = option.GetSwaggerGroupName(), Description = "注册中心客户端相关内置接口" } };
                endpoints.MapGet(MoRegisterCentreConventions.ClientReconnectCentre, async (HttpResponse response, HttpContext context, [FromServices] IRegisterCentreServerConnector connector, [FromServices] IRegisterCentreClient client) =>
                {
                    return await connector.Register(client.GetServiceStatus());
                }).WithName("测试重连配置中心").WithOpenApi(operation =>
                {
                    operation.Summary = "测试重连配置中心";
                    operation.Description = "测试重连配置中心";
                    operation.Tags = tagGroup;
                    return operation;
                });
            });
        }
        
        return Res.Ok();
    }
}
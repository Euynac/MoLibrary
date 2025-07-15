using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using SignalRSwaggerGen.Attributes;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalR(ModuleSignalROption option) : MoModule<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalR;
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() {Name = Option.GetSwaggerGroupName(), Description = "SignalR相关功能扩展"}
            };
            endpoints.MapGet(Option.ServerMethodsRoute, async (HttpResponse response, HttpContext _) =>
            {
                // 遍历所有注册的Hub类型，提取其所有方法，并增加 source 属性（类型名）
                var methods = Option.Hubs
                    .SelectMany(hubType =>
                        hubType.GetMethods()
                            .Where(p => p.DeclaringType == hubType)
                            .Select(p => new
                            {
                                desc = p.GetCustomAttribute<SignalRMethodAttribute>()?.Description ?? p.Name,
                                p.Name,
                                args = p.GetParameters().Select(a => new
                                {
                                    type = a.ParameterType.Name,
                                    a.Name
                                }).ToList(),
                                source = hubType.Name
                            })
                    ).ToList();

                await response.WriteAsJsonAsync(methods);
            }).WithName("获取SignalR所有Server端事件定义").WithOpenApi(operation =>
            {
                operation.Summary = "获取SignalR所有Server端事件定义";
                operation.Description = "获取SignalR所有Server端事件定义";
                operation.Tags = tagGroup;
                return operation;
            });
        });

    }

}
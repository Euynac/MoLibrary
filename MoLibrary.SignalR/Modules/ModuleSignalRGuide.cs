using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Authority.Security;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.SignalR.Implements;
using MoLibrary.SignalR.Interfaces;
using SignalRSwaggerGen;
using SignalRSwaggerGen.Attributes;

namespace MoLibrary.SignalR.Modules;

public class ModuleSignalRGuide : MoModuleGuide<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>
{

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddMoSignalR), nameof(MapMoHub)];
    }

    /// <summary>
    ///     注册SignalR
    /// </summary>
    public ModuleSignalRGuide AddMoSignalR<TIHubOperator, THubOperator, TIContract, TIUser>()
        where THubOperator : class, IMoHubOperator<TIContract, TIUser>, TIHubOperator
        where TIHubOperator : class, IMoHubOperator<TIContract, TIUser>
        where TIContract : IMoHubContract
        where TIUser : IMoCurrentUser
    {
        ConfigureServices(nameof(AddMoSignalR), context =>
        {
            context.Services.AddSingleton<IUserIdProvider, MoUserIdProvider>();
            context.Services.AddSingleton<IMoSignalRConnectionManager, MoSignalRConnectionManager>();
            context.Services.AddTransient<IMoHubOperator<TIContract, TIUser>, THubOperator>();
            context.Services.AddTransient<TIHubOperator, THubOperator>();
            context.Services.AddSignalR(options => { options.EnableDetailedErrors = true; }).AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions); //TODO 可配置
            });
        });
        return this;
    }

    /// <summary>
    ///     配置SignalR Swagger显示
    /// </summary>
    public ModuleSignalRGuide AddMoSignalRSwagger(Action<SignalRSwaggerGenOptions> signalROption)
    {
        ConfigureServices(nameof(AddMoSignalRSwagger), context =>
        {
            context.Services.ConfigureSwaggerGen(o =>
            {
                o.AddSignalRSwaggerGen(signalROption);
            });
        });
        return this;
    }

    /// <summary>
    ///     增加SignalR Hub以及相关接口
    /// </summary>
    public ModuleSignalRGuide MapMoHub<THubServer>([StringSyntax("Route")] string pattern) where THubServer : Hub
    {
     
        ConfigureApplicationBuilder(context =>
        {
            context.ApplicationBuilder.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<THubServer>(pattern);
                var tagGroup = new List<OpenApiTag>
                {
                    new() {Name = context.ModuleOption.GetSwaggerGroupName(), Description = "SignalR相关功能扩展"}
                };
                endpoints.MapGet(context.ModuleOption.ServerMethodsRoute, async (HttpResponse response, HttpContext _) =>
                {
                    var methods = typeof(THubServer).GetMethods().Where(p => p.DeclaringType == typeof(THubServer))
                        .Select(p =>
                            new
                            {
                                desc = p.GetCustomAttribute<SignalRMethodAttribute>()?.Description ?? p.Name,
                                p.Name,
                                args = p.GetParameters().Select(a => new
                                {
                                    type = a.ParameterType.Name,
                                    a.Name
                                }).ToList()
                            }).ToList();

                    await response.WriteAsJsonAsync(methods);
                }).WithName("获取SignalR所有Server端事件定义").WithOpenApi(operation =>
                {
                    operation.Summary = "获取SignalR所有Server端事件定义";
                    operation.Description = "获取SignalR所有Server端事件定义";
                    operation.Tags = tagGroup;
                    return operation;
                });
            });
        });
        return this;
    }

}
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Authority.Security;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.SignalR.Implements;
using MoLibrary.SignalR.Interfaces;
using MoLibrary.SignalR.Services;
using SignalRSwaggerGen;

namespace MoLibrary.SignalR.Modules;

public static class ModuleSignalRBuilderExtensions
{
    public static ModuleSignalRGuide ConfigModuleSignalR(this WebApplicationBuilder builder,
        Action<ModuleSignalROption>? action = null)
    {
        return new ModuleSignalRGuide().Register(action);
    }
}

public class ModuleSignalR(ModuleSignalROption option) : MoModule<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalR;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<MoSignalRManageService>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new()
                {
                    Name = option.GetApiGroupName(),
                    Description = "SignalR管理相关接口"
                }
            };

            // 获取SignalR所有Server端Hub信息
            endpoints.MapGet("/signalr/hubs",
                async ([FromServices] MoSignalRManageService service) =>
                {
                    return (await service.GetHubInfosAsync()).GetResponse();
                })
                .WithName("获取SignalR Hub信息")
                .WithOpenApi(operation =>
                {
                    operation.Summary = "获取SignalR所有Server端Hub信息";
                    operation.Description = "获取所有注册的SignalR Hub的详细信息，包括路由、方法和参数";
                    operation.Tags = tagGroup;
                    return operation;
                });

            // 获取当前所有已连接的SignalR用户
            endpoints.MapGet("/signalr/connected-users",
                async ([FromServices] MoSignalRManageService service) =>
                {
                    return (await service.GetConnectedUsersAsync()).GetResponse();
                })
                .WithName("获取已连接用户")
                .WithOpenApi(operation =>
                {
                    operation.Summary = "获取当前所有已连接的SignalR用户";
                    operation.Description = "获取所有当前连接到SignalR的用户信息，包括连接ID、用户信息和Claims";
                    operation.Tags = tagGroup;
                    return operation;
                });
        });
    }
}

public class ModuleSignalRGuide : MoModuleGuide<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>
{

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddMoSignalR), nameof(MapMoHub)];
    }

    /// <summary>
    ///     注册SignalR，并允许额外配置HubOptions和JsonHubProtocolOptions。
    /// </summary>
    /// <typeparam name="TIHubOperator">Hub操作接口类型。</typeparam>
    /// <typeparam name="THubOperator">Hub操作实现类型。</typeparam>
    /// <typeparam name="TIContract">Hub契约接口类型。</typeparam>
    /// <typeparam name="TIUser">用户接口类型。</typeparam>
    /// <param name="configure">可选的HubOptions配置委托。</param>
    /// <param name="jsonConfigure">可选的JsonHubProtocolOptions配置委托。</param>
    /// <returns>返回当前<see cref="ModuleSignalRGuide"/>实例以便链式调用。</returns>
    public ModuleSignalRGuide AddMoSignalR<TIHubOperator, THubOperator, TIContract, TIUser>(
        Action<HubOptions>? configure = null,
        Action<JsonHubProtocolOptions>? jsonConfigure = null)
        where THubOperator : class, IMoHubOperator<TIContract, TIUser>, TIHubOperator
        where TIHubOperator : class, IMoHubOperator<TIContract, TIUser>
        where TIContract : IMoHubContract
        where TIUser : IMoCurrentUser
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IUserIdProvider, MoUserIdProvider>();
            context.Services.AddSingleton<IMoSignalRConnectionManager, MoSignalRConnectionManager>();
            context.Services.AddTransient<IMoHubOperator<TIContract, TIUser>, THubOperator>();
            context.Services.AddTransient<TIHubOperator, THubOperator>();
            var signalRBuilder = context.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                configure?.Invoke(options);
            });
            signalRBuilder.AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
                jsonConfigure?.Invoke(options);
            });
        });
        return this;
    }

    /// <summary>
    ///     配置SignalR Swagger显示
    /// </summary>
    public ModuleSignalRGuide AddMoSignalRSwagger(Action<SignalRSwaggerGenOptions> signalROption)
    {
        ConfigureServices(context =>
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
        ConfigureModuleOption(option =>
        {
            option.Hubs.Add(new MoHubInfo(typeof(THubServer), pattern));
        }, secondKey: typeof(THubServer).Name);
        ConfigureEndpoints(context =>
        {
            context.ApplicationBuilder.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<THubServer>(pattern);
            });
        }, secondKey: typeof(THubServer).Name);
        return this;
    }

}

/// <summary>
/// SignalR模块配置选项
/// </summary>
public class ModuleSignalROption : MoModuleOptionWithMinimalApi<ModuleSignalR>
{
    /// <summary>
    /// 注册的Hub类型
    /// </summary>
    internal List<MoHubInfo> Hubs { get; set; } = [];
}

/// <summary>
/// Hub信息记录
/// </summary>
public record MoHubInfo(Type HubType, string HubRoute);

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Security;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.SignalR.Implements;
using MoLibrary.SignalR.Interfaces;
using SignalRSwaggerGen;

namespace MoLibrary.SignalR.Modules;

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
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Monica.Authority.Identity.Abstractions;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.JsonSerialization.Services;
using Monica.Core.JsonSerialization.Services.Support;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.SignalR.Implements;
using Monica.SignalR.Interfaces;
using Monica.SignalR.Services;
using SignalRSwaggerGen;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSignalRBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configuring the SignalR module
        /// </summary>
        public static ModuleSignalRGuide AddSignalR(Action<ModuleSignalROption>? action = null)
        {
            return new ModuleSignalRGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.SignalR)]
public class ModuleSignalR(ModuleSignalROption option) : MoModule<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<MoSignalRManageService>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            // Get all server-side Hub information of SignalR
            endpoints.MapGet("/signalr/hubs",
                async ([FromServices] MoSignalRManageService service) =>
                {
                    return (await service.GetHubInfosAsync()).GetResponse();
                })
                .WithName("获取SignalR Hub信息")
                .WithTags(tagName)
                .WithSummary("获取SignalR所有Server端Hub信息")
                .WithDescription("获取所有注册的SignalR Hub的详细信息，包括路由、方法和参数");

            // Get all currently connected SignalR users
            endpoints.MapGet("/signalr/connected-users",
                async ([FromServices] MoSignalRManageService service) =>
                {
                    return (await service.GetConnectedUsersAsync()).GetResponse();
                })
                .WithName("获取已连接用户")
                .WithTags(tagName)
                .WithSummary("获取当前所有已连接的SignalR用户")
                .WithDescription("获取所有当前连接到SignalR的用户信息，包括连接ID、用户信息和Claims");
        });
    }
}

public class ModuleSignalRGuide : MoModuleGuide<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>
{

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddSignalR)];
    }

    /// <summary>
    /// Register SignalR and allow additional configuration of HubOptions and JsonHubProtocolOptions.
    /// </summary>
    /// <typeparam name="TIHubOperator">Hub operation interface type.</typeparam>
    /// <typeparam name="THubOperator">Hub operation implementation type.</typeparam>
    /// <typeparam name="TIContract">Hub contract interface type.</typeparam>
    /// <typeparam name="TIUser">User interface type.</typeparam>
    /// <param name="configure">Optional HubOptions configuration delegate.</param>
    /// <param name="jsonConfigure">Optional JsonHubProtocolOptions configuration delegate.</param>
    /// <returns>Returns the current <see cref="ModuleSignalRGuide"/> instance for chaining calls.</returns>
    public ModuleSignalRGuide AddSignalR<TIHubOperator, THubOperator, TIContract, TIUser>(
        Action<HubOptions>? configure = null,
        Action<JsonHubProtocolOptions>? jsonConfigure = null)
        where THubOperator : class, IMoHubOperator<TIContract, TIUser>, TIHubOperator
        where TIHubOperator : class, IMoHubOperator<TIContract, TIUser>
        where TIContract : IMoHubContract
        where TIUser : ICurrentUser
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
                options.PayloadSerializerOptions.CloneFrom(JsonSerializerOptionsProvider.SharedSerializerOptions);
                jsonConfigure?.Invoke(options);
            });
        });
        return this;
    }

    /// <summary>
    /// Configure SignalR Swagger display
    /// </summary>
    public ModuleSignalRGuide AddSignalRSwagger(Action<SignalRSwaggerGenOptions> signalROption)
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
    /// Add SignalR Hub and related interfaces
    /// </summary>
    public ModuleSignalRGuide MapSignalRHub<THubServer>([StringSyntax("Route")] string pattern) where THubServer : Hub
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
/// SignalR module configuration options
/// </summary>
public class ModuleSignalROption : MoModuleOptionWithMinimalApi<ModuleSignalR>
{
    /// <summary>
    /// Registered Hub type
    /// </summary>
    internal List<MoHubInfo> Hubs { get; set; } = [];
}

/// <summary>
/// Hub information record
/// </summary>
public record MoHubInfo(Type HubType, string HubRoute);

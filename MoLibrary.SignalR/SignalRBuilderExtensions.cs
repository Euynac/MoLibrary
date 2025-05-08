using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Authority.Security;
using MoLibrary.Core.GlobalJson;
using MoLibrary.SignalR.Interfaces;
using MoLibrary.SignalR.Modules;
using SignalRSwaggerGen;
using SignalRSwaggerGen.Attributes;

namespace MoLibrary.SignalR;

public static class SignalRBuilderExtensions
{
    private static bool _hasAdd;

    ///// <summary>
    /////     注册SignalR
    ///// </summary>
    ///// <param name="services"></param>
    //public static void AddDefaultMoSignalR<TIContract, THubServer>(this IServiceCollection services)
    //    where TIContract : class, IMoHubContract where THubServer : MoHubServer<TIContract>
    //{
    //    AddMoSignalR<MoUserHubOperator<TIContract, THubServer>, TIContract, IMoCurrentUser>(services);
    //}

    /// <summary>
    ///     注册SignalR
    /// </summary>
    /// <param name="services"></param>
    public static void AddMoSignalR<TIHubOperator, THubOperator, TIContract, TIUser>(this IServiceCollection services)
        where THubOperator : class, IMoHubOperator<TIContract, TIUser>, TIHubOperator
        where TIHubOperator : class, IMoHubOperator<TIContract, TIUser>
        where TIContract : IMoHubContract
        where TIUser : IMoCurrentUser
    {
        _hasAdd = true;
        services.AddSingleton<IUserIdProvider, MoUserIdProvider>();
        services.AddSingleton<IMoSignalRConnectionManager, MoSignalRConnectionManager>();
        services.AddTransient<IMoHubOperator<TIContract, TIUser>, THubOperator>();
        services.AddTransient<TIHubOperator, THubOperator>();
        services.AddSignalR(options => { options.EnableDetailedErrors = true; }).AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
        });
    }

    /// <summary>
    ///     配置SignalR Swagger显示
    /// </summary>
    public static void AddMoSignalRSwagger(this IServiceCollection services,
        Action<SignalRSwaggerGenOptions> signalROption)
    {
        if (_hasAdd is false) return;

        services.ConfigureSwaggerGen(o =>
        {
            o.AddSignalRSwaggerGen(signalROption);
        });
    }

    /// <summary>
    ///     增加SignalR Hub以及相关接口
    /// </summary>
    public static void MapMoHub<THubServer>(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern, Action<ModuleSignalROption>? optionAction = null) where THubServer : Hub
    {
        if (_hasAdd is false) throw new Exception($"未执行{nameof(AddMoSignalR)}");
        var option = new ModuleSignalROption();
        optionAction?.Invoke(option);

        endpoints.MapHub<THubServer>(pattern);

        var tagGroup = new List<OpenApiTag>
        {
            new() { Name = option.GetSwaggerGroupName(), Description = "SignalR相关功能扩展" }
        };
        endpoints.MapGet(option.ServerMethodsRoute, async (HttpResponse response, HttpContext context) =>
        {
            var methods = typeof(THubServer).GetMethods().Where(p => p.DeclaringType == typeof(THubServer)).Select(p =>
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
    }
}

public class MoUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return new MoCurrentUser(connection.User).Id;
    }
}
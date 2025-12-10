using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.Dapr.Modules;
using MoLibrary.DataChannel.Dashboard.Services;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Services;

namespace MoLibrary.DataChannel.Modules;

public class ModuleDataChannel(ModuleDataChannelOption option)
    : MoModuleWithDependencies<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DataChannel;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        DataChannelCentral.Setting = Option;
        services.AddSingleton<IDataChannelManager, DataChannelManager>();
        services.AddScoped<DataChannelService>();
        // Add the hosted service for channel initialization
        services.AddHostedService<DataChannelInitializerService>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        //use ISetupPipeline
        if (app.ApplicationServices.GetService(typeof(ISetupPipeline)) is ISetupPipeline setup)
        {
            setup.Setup();
        }

        DataChannelCentral.StartBuild(app);

        // Channel initialization is now handled by the hosted service
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        DataChannelCentral.ConfigEndpoints(app);

        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = Option.GetApiGroupName(), Description = "数据通道相关接口" } };

            endpoints.MapGet("/channel/{id}/re-init",
                async ([FromRoute] string id,
                      [FromServices] DataChannelService service,
                      CancellationToken cancellationToken = default) =>
                {
                    var result = await service.ReInitializeChannelAsync(id, cancellationToken);
                    return result.GetResponse();
                })
                .WithName("重新初始化DataChannel").WithOpenApi(operation =>
                {
                    operation.Summary = "重新初始化DataChannel";
                    operation.Description = "对给定ID的DataChannel进行重新初始化操作";
                    operation.Tags = tagGroup;
                    return operation;
                });

            endpoints.MapGet("/channels",
                async ([FromServices] DataChannelService service) =>
                {
                    var result = await service.GetChannelsStatusAsync();
                    return result.GetResponse();
                })
                .WithName("获取DataChannel状态列表").WithOpenApi(operation =>
                {
                    operation.Summary = "获取DataChannel状态列表";
                    operation.Description = "获取所有DataChannel的状态信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            endpoints.MapGet("/channel/{id}/exceptions",
                async ([FromRoute] string id,
                      [FromQuery] int count,
                      [FromServices] DataChannelService service) =>
                {
                    var result = await service.GetChannelExceptionsAsync(id, count);
                    return result.GetResponse();
                })
                .WithName("获取指定DataChannel的异常信息").WithOpenApi(operation =>
                {
                    operation.Summary = "获取指定DataChannel的异常信息";
                    operation.Description = "获取指定DataChannel的异常信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            endpoints.MapGet("/channels/exceptions/summary",
                async ([FromServices] DataChannelService service) =>
                {
                    var result = await service.GetExceptionSummaryAsync();
                    return result.GetResponse();
                })
                .WithName("获取所有DataChannel的异常统计信息").WithOpenApi(operation =>
                {
                    operation.Summary = "获取所有DataChannel的异常统计信息";
                    operation.Description = "获取所有DataChannel的异常统计信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            endpoints.MapDelete("/channel/{id}/exceptions",
                async ([FromRoute] string id,
                      [FromServices] DataChannelService service) =>
                {
                    var result = await service.ClearChannelExceptionsAsync(id);
                    return result.GetResponse();
                })
                .WithName("清空指定DataChannel的异常信息").WithOpenApi(operation =>
                {
                    operation.Summary = "清空指定DataChannel的异常信息";
                    operation.Description = "清空指定DataChannel的异常信息";
                    operation.Tags = tagGroup;
                    return operation;
                });
        });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleObservableInstanceGuide>().Register();
        DependsOnModule<ModuleDaprClientGuide>().Register();
    }
}

public static class ModuleDataChannelBuilderExtensions
{
    public static ModuleDataChannelGuide ConfigModuleDataChannel(this WebApplicationBuilder builder,
        Action<ModuleDataChannelOption>? action = null)
    {
        return new ModuleDataChannelGuide().Register(action);
    }
}


public class ModuleDataChannelGuide : MoModuleGuide<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>
{

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(SetChannelBuilder)];
    }
    public ModuleDataChannelGuide SetChannelBuilder<TBuilderEntrance>()
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton(typeof(ISetupPipeline), typeof(TBuilderEntrance));
        });
        return this;
    }
}

/// <summary>
/// 数据通道配置类
/// 用于配置数据通道的全局设置和选项
/// 实现了IMoModuleOptions接口，支持模块化配置
/// </summary>
public class ModuleDataChannelOption : MoModuleControllerOption<ModuleDataChannel>
{
    /// <summary>
    /// 最近异常保留数量
    /// </summary>
    public int RecentExceptionToKeep { get; set; } = 10;

    /// <summary>
    /// 初始化线程数
    /// </summary>
    public int InitThreadCount { get; set; } = 10;

}
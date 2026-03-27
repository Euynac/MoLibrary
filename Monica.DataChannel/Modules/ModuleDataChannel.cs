using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DataChannel;
using Monica.DataChannel.Interfaces;
using Monica.DataChannel.Services;
using Monica.DataChannel.UIDataChannel.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.DataChannel)]
public class ModuleDataChannel(ModuleDataChannelOption option)
    : MoModule<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        DataChannelCentral.Setting = Option;
        services.AddSingleton<IDataChannelManager, DataChannelManager>();
        services.AddScoped<DataChannelUIService>();
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

        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet("/channel/{id}/re-init",
                async ([FromRoute] string id,
                      [FromServices] DataChannelUIService service,
                      CancellationToken cancellationToken = default) =>
                {
                    var result = await service.ReInitializeChannelAsync(id, cancellationToken);
                    return result.GetResponse();
                })
                .WithName("重新初始化DataChannel")
                .WithTags(tagName)
                .WithSummary("重新初始化DataChannel")
                .WithDescription("对给定ID的DataChannel进行重新初始化操作");

            endpoints.MapGet("/channels",
                async ([FromServices] DataChannelUIService service) =>
                {
                    var result = await service.GetChannelsStatusAsync();
                    return result.GetResponse();
                })
                .WithName("获取DataChannel状态列表")
                .WithTags(tagName)
                .WithSummary("获取DataChannel状态列表")
                .WithDescription("获取所有DataChannel的状态信息");

            endpoints.MapGet("/channel/{id}/exceptions",
                async ([FromRoute] string id,
                      [FromQuery] int count,
                      [FromServices] DataChannelUIService service) =>
                {
                    var result = await service.GetChannelExceptionsAsync(id, count);
                    return result.GetResponse();
                })
                .WithName("获取指定DataChannel的异常信息")
                .WithTags(tagName)
                .WithSummary("获取指定DataChannel的异常信息")
                .WithDescription("获取指定DataChannel的异常信息");

            endpoints.MapGet("/channels/exceptions/summary",
                async ([FromServices] DataChannelUIService service) =>
                {
                    var result = await service.GetExceptionSummaryAsync();
                    return result.GetResponse();
                })
                .WithName("获取所有DataChannel的异常统计信息")
                .WithTags(tagName)
                .WithSummary("获取所有DataChannel的异常统计信息")
                .WithDescription("获取所有DataChannel的异常统计信息");

            endpoints.MapDelete("/channel/{id}/exceptions",
                async ([FromRoute] string id,
                      [FromServices] DataChannelUIService service) =>
                {
                    var result = await service.ClearChannelExceptionsAsync(id);
                    return result.GetResponse();
                })
                .WithName("清空指定DataChannel的异常信息")
                .WithTags(tagName)
                .WithSummary("清空指定DataChannel的异常信息")
                .WithDescription("清空指定DataChannel的异常信息");
        });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleObservableInstanceGuide>().Register();
    }
}

public static class ModuleDataChannelBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the DataChannel module.
        /// </summary>
        public static ModuleDataChannelGuide AddDataChannel(Action<ModuleDataChannelOption>? action = null)
        {
            return new ModuleDataChannelGuide().Register(action);
        }
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
/// Configuration options for the DataChannel module.
/// Defines global settings and module-level behavior for data channels.
/// </summary>
public class ModuleDataChannelOption : MoModuleOptionWithMinimalApi<ModuleDataChannel>
{
    /// <summary>
    /// Gets or sets how many recent exceptions to retain.
    /// </summary>
    public int RecentExceptionToKeep { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of initialization threads.
    /// </summary>
    public int InitThreadCount { get; set; } = 10;

}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using MoLibrary.Configuration.Modules;
using MoLibrary.Configuration.UI.Implements;
using MoLibrary.Configuration.UI.Interfaces;
using MoLibrary.Configuration.UI.Model;
using MoLibrary.Configuration.UI.Services;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.RegisterCentre.Modules;

namespace MoLibrary.Configuration.UI.Modules;

public class ModuleConfigurationDashboard(ModuleConfigurationDashboardOption option)
    : MoModuleWithDependencies<ModuleConfigurationDashboard, ModuleConfigurationDashboardOption, ModuleConfigurationDashboardGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ConfigurationDashboard;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleRegisterCentreGuide>().Register();
        DependsOnModule<ModuleConfigurationGuide>().Register();
        DependsOnModule<ModuleServiceInvocationGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册配置服务
        services.AddScoped<ConfigurationClientService>();
        services.AddScoped<ConfigurationDashboardService>();
        
        if (GetOptions<ModuleRegisterCentreOption>().IsStandaloneMode)
        {
            services.TryAddSingleton<IConfigurationCentreServiceInvoker,
                ConfigurationCentreServiceInvokerStandaloneProvider>();
        }
        else
        {
            services.TryAddSingleton<IConfigurationCentreServiceInvoker,
                ConfigurationCentreServiceInvokerDistributedProvider>();
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (GetOptions<ModuleRegisterCentreOption>().IsCentreServer)
        {
            UseEndpoints(app, endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "配置中心" } };
                endpoints.MapGet(MoConfigurationConventions.DashboardCentreConfigHistory,
                    async ([FromQuery] string? key, [FromQuery] string? appid, [FromQuery] DateTime? start,
                        [FromQuery] DateTime? end, [FromServices] ConfigurationDashboardService dashboardService) =>
                    {
                        return (await dashboardService.GetConfigHistoryAsync(key, appid, start, end)).GetResponse();
                    }).WithName("获取配置类历史").WithOpenApi(operation =>
                    {
                        operation.Summary = "获取配置类历史";
                        operation.Description = "获取配置类历史";
                        operation.Tags = tagGroup;
                        return operation;
                    });


                endpoints.MapPost(MoConfigurationConventions.DashboardCentreConfigRollback,
                    async ([FromBody] RollbackRequest req, [FromServices] ConfigurationDashboardService dashboardService) =>
                    {
                        return (await dashboardService.RollbackConfigAsync(req.Key, req.AppId, req.Version)).GetResponse();

                    }).WithName("回滚配置类").WithOpenApi(operation =>
                    {
                        operation.Summary = "回滚配置类";
                        operation.Description = "回滚配置类";
                        operation.Tags = tagGroup;
                        return operation;
                    });

                endpoints.MapPost(MoConfigurationConventions.DashboardCentreConfigUpdate, async (DtoUpdateConfig req,
                    [FromServices] ConfigurationDashboardService dashboardService) =>
                {
                    return (await dashboardService.UpdateConfigAsync(req)).GetResponse();
                }).WithName("更新指定配置").WithOpenApi(operation =>
                {
                    operation.Summary = "更新指定配置";
                    operation.Description = "更新指定配置";
                    operation.Tags = tagGroup;
                    return operation;
                });

                endpoints.MapGet(MoConfigurationConventions.DashboardCentreOptionItemStatus,
                    async ([FromQuery] string? appid, [FromQuery] string key,
                        [FromServices] ConfigurationDashboardService dashboardService) =>
                    {
                        return (await dashboardService.GetOptionItemStatusAsync(appid, key)).GetResponse();
                    }).WithName("获取指定配置状态").WithOpenApi(operation =>
                    {
                        operation.Summary = "获取指定配置状态";
                        operation.Description = "获取指定配置状态";
                        operation.Tags = tagGroup;
                        return operation;
                    });
                endpoints.MapGet(MoConfigurationConventions.DashboardCentreAllConfigStatus, async (
                    [FromQuery] string? mode,
                    [FromServices] ConfigurationDashboardService dashboardService) =>
                {
                    return (await dashboardService.GetAllConfigStatusAsync(mode)).GetResponse();
                }).WithName("获取所有微服务配置状态").WithOpenApi(operation =>
                {
                    operation.Summary = "获取所有微服务配置状态";
                    operation.Description = "获取所有微服务配置状态";
                    operation.Tags = tagGroup;
                    return operation;
                });
            });
        }
        else
        {
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag>
                    {new() {Name = option.GetApiGroupName(), Description = "热配置面板相关内置接口"}};
                endpoints.MapPost(MoConfigurationConventions.DashboardClientConfigUpdate,
                    async (DtoUpdateConfig req, [FromServices] ConfigurationClientService clientService) =>
                    {
                        return (await clientService.UpdateConfigAsync(req)).GetResponse();
                    }).WithName("配置中心更新指定配置").WithOpenApi(operation =>
                    {
                        operation.Summary = "更新指定配置";
                        operation.Description = "更新指定配置";
                        operation.Tags = tagGroup;
                        return operation;
                    });
            });
        }
    }
}

/// <summary>
/// 回滚请求模型
/// </summary>
public class RollbackRequest
{
    public required string Key { get; set; }
    public required string AppId { get; set; }
    public required string Version { get; set; }
}



public class ModuleConfigurationDashboardGuide : MoModuleGuide<ModuleConfigurationDashboard,
    ModuleConfigurationDashboardOption, ModuleConfigurationDashboardGuide>
{

    public ModuleConfigurationDashboardGuide ConfigCustomDashboard<TDashboard>() where TDashboard : class, IMoConfigurationDashboard
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IMoConfigurationDashboard, TDashboard>();
        }, EMoModuleOrder.PreConfig);
        return this;
    }

    public ModuleConfigurationDashboardGuide SetAsDashboard()
    {
        // Configure RegisterCentre and mark this as a centre server (for endpoint routing)
        DependsOnModule<ModuleRegisterCentreGuide>().Register().SetAsCentreServer();

        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IMoConfigurationDashboard, DefaultArrangeDashboard>();
            context.Services.AddSingleton<MemoryProviderForConfigCentre>();
            context.Services.AddSingleton<IMoConfigurationCentre>(p =>
                p.GetRequiredService<MemoryProviderForConfigCentre>());

            context.Services.TryAddTransient<IMoConfigurationStores, MoConfigurationDefaultMemoryStore>();
            context.Services.AddSingleton<IMoConfigurationModifier, MoConfigurationJsonFileModifier>();
        });
        return this;
    }
    
    public ModuleConfigurationDashboardGuide ConfigCustomStore<TStore>()
        where TStore : class, IMoConfigurationStores
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient<IMoConfigurationStores, TStore>();
        }, EMoModuleOrder.PreConfig);
        return this;
    }
    
}
public static class ModuleConfigurationDashboardBuilderExtensions
{
    public static ModuleConfigurationDashboardGuide ConfigModuleConfigurationDashboard(this WebApplicationBuilder builder,
        Action<ModuleConfigurationDashboardOption>? action = null)
    {
        return new ModuleConfigurationDashboardGuide().Register(action);
    }
}

public class ModuleConfigurationDashboardOption : MoModuleOptionWithMinimalApi<ModuleConfigurationDashboard>
{
}
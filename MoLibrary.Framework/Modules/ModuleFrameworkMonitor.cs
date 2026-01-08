using System.Text.Json.Nodes;
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
using MoLibrary.EventBus.Modules;
using MoLibrary.Framework.Core;
using MoLibrary.Framework.Core.Extensions;
using MoLibrary.Framework.Core.Model;
using MoLibrary.Framework.Services;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Modules;

public static class ModuleFrameworkMonitorBuilderExtensions
{
    public static ModuleFrameworkMonitorGuide ConfigModuleFrameworkMonitor(this WebApplicationBuilder builder,
        Action<ModuleFrameworkMonitorOption>? action = null)
    {
        return new ModuleFrameworkMonitorGuide().Register(action);
    }
}

public class ModuleFrameworkMonitor(ModuleFrameworkMonitorOption option)
    : MoModuleWithDependencies<ModuleFrameworkMonitor, ModuleFrameworkMonitorOption, ModuleFrameworkMonitorGuide>(option), IWantIterateBusinessTypes
{

    public override void ClaimDependencies()
    {
        if (Option.ParseUnitDetails)
        {
            DependsOnModule<ModuleXmlDocumentationGuide>().Register();
        }
        DependsOnModule<ModuleEventBusGuide>().Register();
    }
    private static void InitProjectUnitFactories()
    {
        var type = typeof(ProjectUnit);
        var assembly = type.Assembly;
        var related = type.Namespace;
        assembly.GetTypes()
            .Where(p => p.Namespace == related && p.IsSubclassOf(type) &&
                        p.HasExplicitDefinedStaticConstructor()).Do(p => p.RunStaticConstructor());
    }

    public override EMoModules CurModuleEnum()
    {
        return EMoModules.FrameworkMonitor;
    }

    private IServiceCollection _services = null!;

    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        InitProjectUnitFactories();
     
        ProjectUnit.Option = option;
        
        // 注册框架监控服务
        services.AddScoped<IFrameworkMonitorService, FrameworkMonitorService>();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        return types.ExtractUnitInfo(_services).ExtractEnumInfo();
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        ProjectUnitStores.ProjectUnitsByFullName.Values.Do(p => p.DoingConnect());
        if (option.EnableRequestFilter)
        {
            services.AddRequestFilter();
        }
    }
    class RequestFilterDto
    {
        public List<string>? Urls { get; set; }
        public bool? Disable { get; set; }
    }
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        if (option.EnableRequestFilter)
        {
            app.UseRequestFilter();
        }
    }
    
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "系统框架内置接口" } };

            endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish", 
                async ([FromRoute] string eventKey, 
                      [FromServices] IFrameworkMonitorService frameworkMonitorService,
                      [FromBody] JsonNode eventContent) =>
                {
                    return await frameworkMonitorService.PublishDomainEventAsync(eventKey, eventContent);
                })
                .WithName("测试发布领域事件信息").WithOpenApi(operation =>
                {
                    operation.Summary = "测试发布领域事件信息";
                    operation.Description = "测试发布领域事件信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            if (ProjectUnit.Option.EnableRequestFilter)
            {
                endpoints.MapPost("/framework/request-filter", 
                    async ([FromBody] RequestFilterDto dto, 
                          [FromServices] IFrameworkMonitorService frameworkMonitorService) =>
                    {
                        return await frameworkMonitorService.ManageRequestFilterAsync(dto.Urls, dto.Disable);
                    })
                    .WithName("请求过滤中间件").WithOpenApi(operation =>
                    {
                        operation.Summary = "请求过滤中间件";
                        operation.Description = "请求过滤中间件";
                        operation.Tags = tagGroup;
                        return operation;
                    });
            }


            endpoints.MapGet("/framework/units", 
                async ([FromServices] IFrameworkMonitorService frameworkMonitorService) =>
                {
                    return await frameworkMonitorService.GetAllProjectUnitsAsync();
                })
                .WithName("获取所有项目单元信息").WithOpenApi(operation =>
                {
                    operation.Summary = "获取所有项目单元信息";
                    operation.Description = "获取所有项目单元信息";
                    operation.Tags = tagGroup;
                    return operation;
                });

            endpoints.MapGet("/framework/units/domain-event", 
                async ([FromServices] IFrameworkMonitorService frameworkMonitorService) =>
                {
                    return await frameworkMonitorService.GetDomainEventsAsync();
                })
                .WithName("获取项目领域事件信息").WithOpenApi(operation =>
                {
                    operation.Summary = "获取项目领域事件信息";
                    operation.Description = "获取项目领域事件信息";
                    operation.Tags = tagGroup;
                    return operation;
                });
            endpoints.MapGet("/framework/enum", 
                async ([FromServices] IFrameworkMonitorService frameworkMonitorService, 
                      [FromQuery] string? name = null) =>
                {
                    return await frameworkMonitorService.GetEnumInfoAsync(name);
                })
                .WithName("获取项目枚举信息").WithOpenApi(operation =>
                {
                    operation.Summary = "获取项目枚举信息";
                    operation.Description = "获取项目枚举信息";
                    operation.Tags = tagGroup;
                    return operation;
                });
        });
    }
}

public class ModuleFrameworkMonitorGuide : MoModuleGuide<ModuleFrameworkMonitor, ModuleFrameworkMonitorOption,
    ModuleFrameworkMonitorGuide>
{


}

public class ModuleFrameworkMonitorOption : MoModuleOptionWithMinimalApi<ModuleFrameworkMonitor>
{
    /// <summary>
    /// 惯例命名设置
    /// </summary>
    public UnitNameConventionOptions ConventionOptions { get; set; } = new();

    /// <summary>
    /// 开启请求过滤器
    /// </summary>
    public bool EnableRequestFilter { get; set; }

    /// <summary>
    /// 是否解析项目单元具体信息（如XML文档注释等）
    /// </summary>
    public bool ParseUnitDetails { get; set; } = true;
}

public class UnitNameConventionOptions
{
    public Dictionary<EProjectUnitType, UnitNameConventionOption> Dict { get; set; } = [];
    /// <summary>
    /// 全局惯例命名模式
    /// </summary>
    public ENameConventionMode NameConventionMode { get; set; } = ENameConventionMode.Warning;

    /// <summary>
    /// 使用惯例名称检查
    /// </summary>
    public bool EnableNameConvention { get; set; }
}

public class UnitNameConventionOption
{
    /// <summary>
    /// 后缀名
    /// </summary>
    public string? Postfix { get; set; }
    /// <summary>
    /// 前缀名
    /// </summary>
    public string? Prefix { get; set; }
    /// <summary>
    /// 包含名
    /// </summary>
    public string? Contains { get; set; }
    /// <summary>
    /// 命名空间包含，如必须放入文件夹名
    /// </summary>
    public string? NamespaceContains { get; set; }
    /// <summary>
    /// 惯例命名模式，不设置使用全局模式
    /// </summary>
    public ENameConventionMode? NameConventionMode { get; set; } = ENameConventionMode.Warning;

    public override string ToString()
    {
        return $"{Postfix?.Be("后缀：{0}\n", true)}{Prefix?.Be("前缀：{0}\n", true)}{Contains?.Be("包含：{0}", true)}{NamespaceContains?.Be("命名空间包含：{0}", true)}".TrimEnd();
    }
}

public enum ENameConventionMode
{
    /// <summary>
    /// 警告模式，仅提醒
    /// </summary>
    Warning,
    /// <summary>
    /// 严格模式，错误直接无法运行
    /// </summary>
    Strict,
    /// <summary>
    /// 禁用Convention
    /// </summary>
    Disable
}

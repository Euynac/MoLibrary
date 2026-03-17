using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Framework.Core;
using Monica.Framework.Core.Extensions;
using Monica.Framework.Core.Model;
using Monica.Framework.Services;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleFrameworkMonitorBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 FrameworkMonitor 模块
        /// </summary>
        public static ModuleFrameworkMonitorGuide AddFrameworkMonitor(Action<ModuleFrameworkMonitorOption>? action = null)
        {
            return new ModuleFrameworkMonitorGuide().Register(action);
        }
    }
}

public class ModuleFrameworkMonitor(ModuleFrameworkMonitorOption option)
    : MoModule<ModuleFrameworkMonitor, ModuleFrameworkMonitorOption, ModuleFrameworkMonitorGuide>(option), IWantIterateBusinessTypes
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

    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.FrameworkMonitor;
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
            var tagName = option.GetApiGroupName();

            endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish",
                async ([FromRoute] string eventKey,
                      [FromServices] IFrameworkMonitorService frameworkMonitorService,
                      [FromBody] JsonNode eventContent) =>
                {
                    return await frameworkMonitorService.PublishDomainEventAsync(eventKey, eventContent);
                })
                .WithName("测试发布领域事件信息")
                .WithTags(tagName)
                .WithSummary("测试发布领域事件信息")
                .WithDescription("测试发布领域事件信息");

            if (ProjectUnit.Option.EnableRequestFilter)
            {
                endpoints.MapPost("/framework/request-filter",
                    async ([FromBody] RequestFilterDto dto,
                          [FromServices] IFrameworkMonitorService frameworkMonitorService) =>
                    {
                        return await frameworkMonitorService.ManageRequestFilterAsync(dto.Urls, dto.Disable);
                    })
                    .WithName("请求过滤中间件")
                    .WithTags(tagName)
                    .WithSummary("请求过滤中间件")
                    .WithDescription("请求过滤中间件");
            }


            endpoints.MapGet("/framework/units",
                async ([FromServices] IFrameworkMonitorService frameworkMonitorService) =>
                {
                    return await frameworkMonitorService.GetAllProjectUnitsAsync();
                })
                .WithName("获取所有项目单元信息")
                .WithTags(tagName)
                .WithSummary("获取所有项目单元信息")
                .WithDescription("获取所有项目单元信息");

            endpoints.MapGet("/framework/units/domain-event",
                async ([FromServices] IFrameworkMonitorService frameworkMonitorService) =>
                {
                    return await frameworkMonitorService.GetDomainEventsAsync();
                })
                .WithName("获取项目领域事件信息")
                .WithTags(tagName)
                .WithSummary("获取项目领域事件信息")
                .WithDescription("获取项目领域事件信息");

            endpoints.MapGet("/framework/enum",
                async ([FromServices] IFrameworkMonitorService frameworkMonitorService,
                      [FromQuery] string? name = null) =>
                {
                    return await frameworkMonitorService.GetEnumInfoAsync(name);
                })
                .WithName("获取项目枚举信息")
                .WithTags(tagName)
                .WithSummary("获取项目枚举信息")
                .WithDescription("获取项目枚举信息");
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

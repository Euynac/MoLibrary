using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Framework.ProjectUnits.Facades;
using Monica.Framework.ProjectUnits.Models;
using Monica.Framework.ProjectUnits.Providers.AspNetCore;
using Monica.Framework.ProjectUnits.Services;
using Monica.Framework.ProjectUnits.Services.Support;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProjectUnitsBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the project-units module.
        /// </summary>
        public static ModuleProjectUnitsGuide AddProjectUnits(Action<ModuleProjectUnitsOption>? action = null)
        {
            return new ModuleProjectUnitsGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.ProjectUnits)]
public class ModuleProjectUnits(ModuleProjectUnitsOption option)
    : MoModule<ModuleProjectUnits, ModuleProjectUnitsOption, ModuleProjectUnitsGuide>(option), IWantIterateBusinessTypes
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

    private IServiceCollection _services = null!;

    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        InitProjectUnitFactories();
     
        ProjectUnit.Option = option;
        
        services.AddScoped<ProjectUnitCatalogService>();
        services.AddScoped<ProjectUnitsFacade>();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        return types.ExtractUnitInfo(_services).ExtractEnumInfo();
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        ProjectUnitRegistry.ProjectUnitsByFullName.Values.Do(p => p.DoingConnect());
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
                      [FromServices] ProjectUnitsFacade projectUnitsFacade,
                      [FromBody] JsonNode eventContent) =>
                {
                    return await projectUnitsFacade.PublishDomainEventAsync(eventKey, eventContent);
                })
                .WithName("测试发布领域事件信息")
                .WithTags(tagName)
                .WithSummary("测试发布领域事件信息")
                .WithDescription("测试发布领域事件信息");

            if (ProjectUnit.Option.EnableRequestFilter)
            {
                endpoints.MapPost("/framework/request-filter",
                    async ([FromBody] RequestFilterDto dto,
                          [FromServices] ProjectUnitsFacade projectUnitsFacade) =>
                    {
                        return await projectUnitsFacade.ManageRequestFilterAsync(dto.Urls, dto.Disable);
                    })
                    .WithName("请求过滤中间件")
                    .WithTags(tagName)
                    .WithSummary("请求过滤中间件")
                    .WithDescription("请求过滤中间件");
            }

            endpoints.MapGet("/framework/units",
                async ([FromServices] ProjectUnitsFacade projectUnitsFacade) =>
                {
                    return await projectUnitsFacade.GetAllProjectUnitsAsync();
                })
                .WithName("获取所有项目单元信息")
                .WithTags(tagName)
                .WithSummary("获取所有项目单元信息")
                .WithDescription("获取所有项目单元信息");

            endpoints.MapGet("/framework/units/domain-event",
                async ([FromServices] ProjectUnitsFacade projectUnitsFacade) =>
                {
                    return await projectUnitsFacade.GetDomainEventsAsync();
                })
                .WithName("获取项目领域事件信息")
                .WithTags(tagName)
                .WithSummary("获取项目领域事件信息")
                .WithDescription("获取项目领域事件信息");

            endpoints.MapGet("/framework/enum",
                async ([FromServices] ProjectUnitsFacade projectUnitsFacade,
                      [FromQuery] string? name = null) =>
                {
                    return await projectUnitsFacade.GetEnumInfoAsync(name);
                })
                .WithName("获取项目枚举信息")
                .WithTags(tagName)
                .WithSummary("获取项目枚举信息")
                .WithDescription("获取项目枚举信息");
        });
    }
}

public class ModuleProjectUnitsGuide : MoModuleGuide<ModuleProjectUnits, ModuleProjectUnitsOption,
    ModuleProjectUnitsGuide>
{

}

public class ModuleProjectUnitsOption : MoModuleOptionWithMinimalApi<ModuleProjectUnits>
{
    /// <summary>
    /// Convention naming settings
    /// </summary>
    public ProjectUnitNamingOptions ConventionOptions { get; set; } = new();

    /// <summary>
    /// Enable request filter
    /// </summary>
    public bool EnableRequestFilter { get; set; }

    /// <summary>
    /// Whether to parse project unit specific information (such as XML document comments, etc.)
    /// </summary>
    public bool ParseUnitDetails { get; set; } = true;
}

public class ProjectUnitNamingOptions
{
    public Dictionary<EProjectUnitType, ProjectUnitNamingRule> Dict { get; set; } = [];
    /// <summary>
    /// Global convention naming patterns
    /// </summary>
    public ENameConventionMode NameConventionMode { get; set; } = ENameConventionMode.Warning;

    /// <summary>
    /// Use convention name checking
    /// </summary>
    public bool EnableNameConvention { get; set; }
}

public class ProjectUnitNamingRule
{
    /// <summary>
    /// suffix
    /// </summary>
    public string? Postfix { get; set; }
    /// <summary>
    /// prefix name
    /// </summary>
    public string? Prefix { get; set; }
    /// <summary>
    /// Contains name
    /// </summary>
    public string? Contains { get; set; }
    /// <summary>
    /// Namespace contains, if you must put the folder name
    /// </summary>
    public string? NamespaceContains { get; set; }
    /// <summary>
    /// Conventional naming pattern, use global pattern if not set
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
    /// Warning mode, reminder only
    /// </summary>
    Warning,
    /// <summary>
    /// Strict mode, error directly cannot run
    /// </summary>
    Strict,
    /// <summary>
    /// DisableConvention
    /// </summary>
    Disable
}

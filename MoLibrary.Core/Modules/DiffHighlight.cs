using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features.MoDiffHighlight;
using MoLibrary.Core.Features.MoDiffHighlight.Algorithms;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using MoLibrary.Core.Features.MoDiffHighlight.Renderers;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// 差异对比高亮模块
/// </summary>
public class ModuleDiffHighlight(ModuleDiffHighlightOption option) : MoModule<ModuleDiffHighlight, ModuleDiffHighlightOption, ModuleDiffHighlightGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DiffHighlight;
    }
    
    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        Logger.LogInformation("正在配置差异对比高亮模块服务");
        
        // 注册核心服务
        services.AddScoped<IMoDiffHighlight, DefaultDiffHighlight>();
        services.AddScoped<DiffHighlightService>();
        
        // 注册算法
        services.AddTransient<IDiffAlgorithm, SimpleMyersDiffAlgorithm>();
        
        // 注册渲染器
        services.AddTransient<HtmlDiffRenderer>();
        services.AddTransient<MarkdownDiffRenderer>();
        services.AddTransient<PlainTextDiffRenderer>();
        
        // 注册自定义渲染器（如果有）
        if (option.CustomRendererFactory != null)
        {
            services.AddSingleton<IDiffHighlightRenderer>(provider => option.CustomRendererFactory());
            Logger.LogDebug("已注册自定义渲染器");
        }
        
        Logger.LogInformation("差异对比高亮模块服务配置完成");
    }
    
    /// <summary>
    /// 配置端点
    /// </summary>
    /// <param name="app">应用构建器</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (option.GetIsControllerDisabled())
        {
            Logger.LogDebug("差异对比高亮模块控制器已禁用，跳过端点配置");
            return;
        }
        
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetApiGroupName(), Description = "文本差异对比高亮相关接口" }
            };
            
            // 文本差异对比端点
            endpoints.MapPost("/diff-highlight", async (DiffHighlightRequest request, DiffHighlightService service) =>
            {
                var result = await service.HighlightAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("文本差异对比")
            .WithOpenApi(operation =>
            {
                operation.Summary = "执行文本差异对比并生成高亮结果";
                operation.Description = "比较两个文本并生成带高亮的差异结果，支持多种输出格式";
                operation.Tags = tagGroup;
                return operation;
            });
            
            // 差异统计信息端点
            endpoints.MapPost("/diff-highlight/statistics", async (DiffHighlightRequest request, DiffHighlightService service) =>
            {
                var result = await service.GetStatisticsAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("获取差异统计信息")
            .WithOpenApi(operation =>
            {
                operation.Summary = "获取文本差异统计信息";
                operation.Description = "获取两个文本之间的差异统计数据，如新增行数、删除行数等";
                operation.Tags = tagGroup;
                return operation;
            });
            
            // 文本相同性检查端点
            endpoints.MapPost("/diff-highlight/identical", async (DiffHighlightRequest request, DiffHighlightService service) =>
            {
                var result = await service.IsIdenticalAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("检查文本相同性")
            .WithOpenApi(operation =>
            {
                operation.Summary = "检查两个文本是否相同";
                operation.Description = "快速检查两个文本是否完全相同（考虑配置的忽略选项）";
                operation.Tags = tagGroup;
                return operation;
            });
        });
        
        Logger.LogInformation("差异对比高亮模块端点配置完成");
    }
}

/// <summary>
/// 差异对比高亮模块扩展方法
/// </summary>
public static class ModuleDiffHighlightBuilderExtensions
{
    /// <summary>
    /// 配置差异对比高亮模块
    /// </summary>
    /// <param name="builder">Web应用构建器</param>
    /// <param name="action">配置委托</param>
    /// <returns>模块引导实例</returns>
    public static ModuleDiffHighlightGuide ConfigMoDiffHighlight(this WebApplicationBuilder builder,
        Action<ModuleDiffHighlightOption>? action = null)
    {
        return new ModuleDiffHighlightGuide().Register(action);
    }
}

/// <summary>
/// 差异对比请求模型
/// </summary>
public class DiffHighlightRequest
{
    /// <summary>
    /// 原始文本
    /// </summary>
    public string OldText { get; set; } = string.Empty;
    
    /// <summary>
    /// 新文本
    /// </summary>
    public string NewText { get; set; } = string.Empty;
    
    /// <summary>
    /// 配置选项
    /// </summary>
    public DiffHighlightOptions? Options { get; set; }
}

/// <summary>
/// 差异对比高亮模块配置引导
/// </summary>
public class ModuleDiffHighlightGuide : MoModuleGuide<ModuleDiffHighlight, ModuleDiffHighlightOption, ModuleDiffHighlightGuide>
{

}


/// <summary>
/// 差异对比高亮模块配置选项
/// </summary>
public class ModuleDiffHighlightOption : MoModuleControllerOption<ModuleDiffHighlight>
{
    /// <summary>
    /// 默认对比模式
    /// </summary>
    public EDiffHighlightMode DefaultMode { get; set; } = EDiffHighlightMode.Line;

    /// <summary>
    /// 默认输出格式
    /// </summary>
    public EDiffOutputFormat DefaultOutputFormat { get; set; } = EDiffOutputFormat.Html;

    /// <summary>
    /// 默认样式配置
    /// </summary>
    public DiffHighlightStyle DefaultStyle { get; set; } = new();

    /// <summary>
    /// 是否忽略空白字符
    /// </summary>
    public bool IgnoreWhitespace { get; set; } = false;

    /// <summary>
    /// 是否忽略大小写
    /// </summary>
    public bool IgnoreCase { get; set; } = false;

    /// <summary>
    /// 默认上下文行数
    /// </summary>
    public int DefaultContextLines { get; set; } = 3;

    /// <summary>
    /// 最大字符级差异长度
    /// </summary>
    public int MaxCharacterDiffLength { get; set; } = 1000;

    /// <summary>
    /// 自定义渲染器工厂函数
    /// </summary>
    public Func<IDiffHighlightRenderer>? CustomRendererFactory { get; set; }

    /// <summary>
    /// 是否启用性能监控
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// 获取默认配置选项
    /// </summary>
    /// <returns>默认配置选项</returns>
    public DiffHighlightOptions GetDefaultOptions()
    {
        return new DiffHighlightOptions
        {
            Mode = DefaultMode,
            OutputFormat = DefaultOutputFormat,
            IgnoreWhitespace = IgnoreWhitespace,
            IgnoreCase = IgnoreCase,
            ContextLines = DefaultContextLines,
            MaxCharacterDiffLength = MaxCharacterDiffLength,
            Style = DefaultStyle
        };
    }
}
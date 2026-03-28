using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Features.MoDiffHighlight;
using Monica.Core.Features.MoDiffHighlight.Algorithms;
using Monica.Core.Features.MoDiffHighlight.Models;
using Monica.Core.Features.MoDiffHighlight.Renderers;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Diff highlight module.
/// </summary>
[ModuleKey(EMoModuleKey.DiffHighlight)]
public class ModuleDiffHighlight(ModuleDiffHighlightOption option) : MoModule<ModuleDiffHighlight, ModuleDiffHighlightOption, ModuleDiffHighlightGuide>(option)
{
    
    /// <summary>
    /// Configures services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register core services.
        services.AddScoped<IMoDiffHighlight, DefaultDiffHighlight>();
        services.AddScoped<DiffHighlightService>();
        
        // Register diff algorithms.
        services.AddTransient<IDiffAlgorithm, SimpleMyersDiffAlgorithm>();
        
        // Register built-in renderers.
        services.AddTransient<HtmlDiffRenderer>();
        services.AddTransient<MarkdownDiffRenderer>();
        services.AddTransient<PlainTextDiffRenderer>();
        
        // Register a custom renderer when one is provided.
        if (option.CustomRendererFactory != null)
        {
            services.AddSingleton<IDiffHighlightRenderer>(provider => option.CustomRendererFactory());
            Logger.LogDebug("已注册自定义渲染器");
        }
    }
    
    /// <summary>
    /// Configures endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            // Text diff endpoint.
            endpoints.MapPost("/diff-highlight", async (DiffHighlightRequest request, DiffHighlightService service) =>
            {
                var result = await service.HighlightAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("文本差异对比")
            .WithTags(tagName)
            .WithSummary("执行文本差异对比并生成高亮结果")
            .WithDescription("比较两个文本并生成带高亮的差异结果，支持多种输出格式");

            // Diff statistics endpoint.
            endpoints.MapPost("/diff-highlight/statistics", async (DiffHighlightRequest request, DiffHighlightService service) =>
            {
                var result = await service.GetStatisticsAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("获取差异统计信息")
            .WithTags(tagName)
            .WithSummary("获取文本差异统计信息")
            .WithDescription("获取两个文本之间的差异统计数据，如新增行数、删除行数等");

            // Text identity check endpoint.
            endpoints.MapPost("/diff-highlight/identical", async (DiffHighlightRequest request, DiffHighlightService service) =>
            {
                var result = await service.IsIdenticalAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("检查文本相同性")
            .WithTags(tagName)
            .WithSummary("检查两个文本是否相同")
            .WithDescription("快速检查两个文本是否完全相同（考虑配置的忽略选项）");
        });
    }
}

public static class ModuleDiffHighlightBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the DiffHighlight module.
        /// </summary>
        public static ModuleDiffHighlightGuide AddDiffHighlight(Action<ModuleDiffHighlightOption>? action = null)
        {
            return new ModuleDiffHighlightGuide().Register(action);
        }
    }
}

/// <summary>
/// Diff request model.
/// </summary>
public class DiffHighlightRequest
{
    /// <summary>
    /// Original text.
    /// </summary>
    public string OldText { get; set; } = string.Empty;
    
    /// <summary>
    /// Updated text.
    /// </summary>
    public string NewText { get; set; } = string.Empty;
    
    /// <summary>
    /// Diff options.
    /// </summary>
    public DiffHighlightOptions? Options { get; set; }
}

/// <summary>
/// Configuration guide for the diff highlight module.
/// </summary>
public class ModuleDiffHighlightGuide : MoModuleGuide<ModuleDiffHighlight, ModuleDiffHighlightOption, ModuleDiffHighlightGuide>
{

}

/// <summary>
/// Configuration options for the diff highlight module.
/// </summary>
public class ModuleDiffHighlightOption : MoModuleOptionWithMinimalApi<ModuleDiffHighlight>
{
    /// <summary>
    /// Default diff mode.
    /// </summary>
    public EDiffHighlightMode DefaultMode { get; set; } = EDiffHighlightMode.Line;

    /// <summary>
    /// Default output format.
    /// </summary>
    public EDiffOutputFormat DefaultOutputFormat { get; set; } = EDiffOutputFormat.Html;

    /// <summary>
    /// Default style configuration.
    /// </summary>
    public DiffHighlightStyle DefaultStyle { get; set; } = new();

    /// <summary>
    /// Ignores whitespace.
    /// </summary>
    public bool IgnoreWhitespace { get; set; } = false;

    /// <summary>
    /// Ignores character casing.
    /// </summary>
    public bool IgnoreCase { get; set; } = false;

    /// <summary>
    /// Default number of context lines.
    /// </summary>
    public int DefaultContextLines { get; set; } = 3;

    /// <summary>
    /// Maximum character-level diff length.
    /// </summary>
    public int MaxCharacterDiffLength { get; set; } = 1000;

    /// <summary>
    /// Factory for a custom renderer.
    /// </summary>
    public Func<IDiffHighlightRenderer>? CustomRendererFactory { get; set; }

    /// <summary>
    /// Enables performance monitoring.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Builds the default runtime diff options.
    /// </summary>
    /// <returns>The default diff options.</returns>
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

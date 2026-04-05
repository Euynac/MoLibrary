using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.UI.UIDiffHighlight.Abstractions;
using Monica.UI.UIDiffHighlight.Abstractions.Internal;
using Monica.UI.UIDiffHighlight.Facades;
using Monica.UI.UIDiffHighlight.Models;
using Monica.UI.UIDiffHighlight.Services;
using Monica.UI.UIDiffHighlight.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Registers the DiffHighlight mixed module.
/// </summary>
[ModuleKey(BuiltInModuleKey.DiffHighlight)]
public class ModuleDiffHighlight(ModuleDiffHighlightOption option) : ModuleBase<ModuleDiffHighlight, ModuleDiffHighlightOption, ModuleDiffHighlightGuide>(option)
{
    /// <summary>
    /// Registers the diff highlighting services, facade, and rendering strategies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDiffHighlight, DiffHighlightService>();
        services.AddScoped<DiffHighlightFacade>();

        services.AddTransient<IDiffAlgorithm, SimpleMyersDiffAlgorithm>();
        services.AddTransient<HtmlDiffRenderer>();
        services.AddTransient<MarkdownDiffRenderer>();
        services.AddTransient<PlainTextDiffRenderer>();

        if (option.CustomRendererFactory != null)
        {
            services.AddSingleton<IDiffHighlightRenderer>(provider => option.CustomRendererFactory());
            Logger.LogDebug("Registered a custom diff highlight renderer.");
        }
    }

    /// <summary>
    /// Registers the diff highlight minimal API endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapPost("/diff-highlight", async (DiffHighlightRequest request, DiffHighlightFacade service) =>
            {
                var result = await service.HighlightAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("文本差异对比")
            .WithTags(tagName)
            .WithSummary("执行文本差异对比并生成高亮结果")
            .WithDescription("比较两个文本并生成带高亮的差异结果，支持多种输出格式");

            endpoints.MapPost("/diff-highlight/statistics", async (DiffHighlightRequest request, DiffHighlightFacade service) =>
            {
                var result = await service.GetStatisticsAsync(request.OldText, request.NewText, request.Options);
                return result.GetResponse();
            })
            .WithName("获取差异统计信息")
            .WithTags(tagName)
            .WithSummary("获取文本差异统计信息")
            .WithDescription("获取两个文本之间的差异统计数据，如新增行数、删除行数等");

            endpoints.MapPost("/diff-highlight/identical", async (DiffHighlightRequest request, DiffHighlightFacade service) =>
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
/// Request payload for diff highlight endpoints.
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
public class ModuleDiffHighlightGuide : ModuleGuide<ModuleDiffHighlight, ModuleDiffHighlightOption, ModuleDiffHighlightGuide>
{

}

/// <summary>
/// Configuration options for the diff highlight module.
/// </summary>
public class ModuleDiffHighlightOption : MinimalApiModuleOptions<ModuleDiffHighlight>
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

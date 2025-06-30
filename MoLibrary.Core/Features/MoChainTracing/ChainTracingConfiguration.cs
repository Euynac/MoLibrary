using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Features.MoChainTracing;

/// <summary>
/// 调用链追踪配置选项
/// </summary>
public class ChainTracingOptions
{
    /// <summary>
    /// 是否启用自动调用链追踪
    /// </summary>
    public bool EnableAutoTracing { get; set; } = true;

    /// <summary>
    /// 是否启用中间件
    /// </summary>
    public bool EnableMiddleware { get; set; } = true;

    /// <summary>
    /// 是否启用 ActionFilter
    /// </summary>
    public bool EnableActionFilter { get; set; } = true;

    /// <summary>
    /// 是否记录请求参数
    /// </summary>
    public bool LogRequestParameters { get; set; } = false;

    /// <summary>
    /// 是否记录响应内容
    /// </summary>
    public bool LogResponseContent { get; set; } = false;

    /// <summary>
    /// 最大调用链深度（防止无限递归）
    /// </summary>
    public int MaxChainDepth { get; set; } = 50;

    /// <summary>
    /// 最大节点数量（防止内存泄漏）
    /// </summary>
    public int MaxNodeCount { get; set; } = 1000;

    /// <summary>
    /// 是否在 ExtraInfo 中包含调用链的详细信息
    /// </summary>
    public bool IncludeDetailedChainInfo { get; set; } = true;

    /// <summary>
    /// 是否在 ExtraInfo 中包含调用链的汇总信息
    /// </summary>
    public bool IncludeChainSummary { get; set; } = true;

    /// <summary>
    /// 需要跳过的路径模式（正则表达式）
    /// </summary>
    public List<string> SkipPathPatterns { get; set; } =
    [
        @"^/health.*",
        @"^/swagger.*",
        @"^/favicon\.ico",
        @"^/_vs/.*"
    ];

    /// <summary>
    /// 需要跳过的控制器名称
    /// </summary>
    public List<string> SkipControllers { get; set; } =
    [
        "HealthController",
        "DiagnosticsController"
    ];
}

/// <summary>
/// 调用链追踪服务注册扩展
/// </summary>
public static class ChainTracingServiceCollectionExtensions
{
    /// <summary>
    /// 添加调用链追踪服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMoChainTracing(this IServiceCollection services, 
        Action<ChainTracingOptions>? configureOptions = null)
    {
        // 注册配置选项
        var options = new ChainTracingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // 根据配置决定是否启用调用链追踪
        if (options.EnableAutoTracing)
        {
            // 注册调用链追踪服务
            services.AddSingleton<IMoChainTracing, AsyncLocalMoChainTracing>();
        }
        else
        {
            // 注册空实现
            services.AddSingleton<IMoChainTracing>(_ => EmptyChainTracing.Instance);
        }

        // 根据配置注册 ActionFilter
        if (options.EnableActionFilter)
        {
            services.AddScoped<ChainTracingActionFilter>();
            services.AddScoped<AutoChainTracingActionFilter>();
        }

        return services;
    }

    /// <summary>
    /// 禁用调用链追踪服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection DisableMoChainTracing(this IServiceCollection services)
    {
        // 注册禁用配置
        var options = new ChainTracingOptions
        {
            EnableAutoTracing = false,
            EnableMiddleware = false,
            EnableActionFilter = false
        };
        services.AddSingleton(options);

        // 注册空实现
        services.AddSingleton<IMoChainTracing>(_ => EmptyChainTracing.Instance);

        return services;
    }

    /// <summary>
    /// 添加调用链追踪 MVC 配置
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMoChainTracingMvc(this IServiceCollection services,
        Action<ChainTracingOptions>? configureOptions = null)
    {
        services.AddMoChainTracing(configureOptions);

        services.AddMvc(options =>
        {
            // 添加全局 ActionFilter
            options.Filters.Add<AutoChainTracingActionFilter>();
        });

        return services;
    }
}

/// <summary>
/// 调用链追踪应用构建扩展
/// </summary>
public static class ChainTracingApplicationBuilderExtensions
{
    /// <summary>
    /// 使用调用链追踪
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseMoChainTracing(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<ChainTracingOptions>();
        
        if (options?.EnableMiddleware == true)
        {
            app.UseMiddleware<ChainTracingMiddleware>();
        }

        return app;
    }

    /// <summary>
    /// 使用调用链追踪（带配置）
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseMoChainTracing(this IApplicationBuilder app,
        Action<ChainTracingOptions> configureOptions)
    {
        var options = app.ApplicationServices.GetService<ChainTracingOptions>() ?? new ChainTracingOptions();
        configureOptions.Invoke(options);

        if (options.EnableMiddleware)
        {
            app.UseMiddleware<ChainTracingMiddleware>();
        }

        return app;
    }
}

/// <summary>
/// 调用链追踪配置构建器
/// </summary>
public class ChainTracingConfigurationBuilder
{
    private readonly ChainTracingOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">配置选项</param>
    public ChainTracingConfigurationBuilder(ChainTracingOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// 启用自动追踪
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder EnableAutoTracing()
    {
        _options.EnableAutoTracing = true;
        return this;
    }

    /// <summary>
    /// 禁用自动追踪
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder DisableAutoTracing()
    {
        _options.EnableAutoTracing = false;
        return this;
    }

    /// <summary>
    /// 启用中间件
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder EnableMiddleware()
    {
        _options.EnableMiddleware = true;
        return this;
    }

    /// <summary>
    /// 禁用中间件
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder DisableMiddleware()
    {
        _options.EnableMiddleware = false;
        return this;
    }

    /// <summary>
    /// 启用 ActionFilter
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder EnableActionFilter()
    {
        _options.EnableActionFilter = true;
        return this;
    }

    /// <summary>
    /// 禁用 ActionFilter
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder DisableActionFilter()
    {
        _options.EnableActionFilter = false;
        return this;
    }

    /// <summary>
    /// 设置最大调用链深度
    /// </summary>
    /// <param name="maxDepth">最大深度</param>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder SetMaxChainDepth(int maxDepth)
    {
        _options.MaxChainDepth = maxDepth;
        return this;
    }

    /// <summary>
    /// 设置最大节点数量
    /// </summary>
    /// <param name="maxNodes">最大节点数</param>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder SetMaxNodeCount(int maxNodes)
    {
        _options.MaxNodeCount = maxNodes;
        return this;
    }

    /// <summary>
    /// 添加跳过路径模式
    /// </summary>
    /// <param name="pattern">路径模式（正则表达式）</param>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder AddSkipPathPattern(string pattern)
    {
        _options.SkipPathPatterns.Add(pattern);
        return this;
    }

    /// <summary>
    /// 添加跳过控制器
    /// </summary>
    /// <param name="controllerName">控制器名称</param>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder AddSkipController(string controllerName)
    {
        _options.SkipControllers.Add(controllerName);
        return this;
    }

    /// <summary>
    /// 启用请求参数记录
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder LogRequestParameters()
    {
        _options.LogRequestParameters = true;
        return this;
    }

    /// <summary>
    /// 启用响应内容记录
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder LogResponseContent()
    {
        _options.LogResponseContent = true;
        return this;
    }

    /// <summary>
    /// 包含详细调用链信息
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder IncludeDetailedChainInfo()
    {
        _options.IncludeDetailedChainInfo = true;
        return this;
    }

    /// <summary>
    /// 只包含调用链汇总信息
    /// </summary>
    /// <returns>配置构建器</returns>
    public ChainTracingConfigurationBuilder OnlyChainSummary()
    {
        _options.IncludeDetailedChainInfo = false;
        _options.IncludeChainSummary = true;
        return this;
    }
} 
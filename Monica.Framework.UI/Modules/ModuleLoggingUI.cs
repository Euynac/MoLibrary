using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Framework.UI.UILogging.Models;
using Monica.Framework.UI.UILogging.Services;
using Monica.Framework.UI.Pages;
using Monica.Tool.MoResponse;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLoggingUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 LoggingUI 模块
        /// </summary>
        public static ModuleLoggingUIGuide AddLoggingUI(Action<ModuleLoggingUIOption>? action = null)
        {
            return new ModuleLoggingUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Logging UI 模块实现
/// </summary>
public class ModuleLoggingUI(ModuleLoggingUIOption option)
    : MoModule<ModuleLoggingUI, ModuleLoggingUIOption, ModuleLoggingUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.LoggingUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ScreenLogBuffer>();
        services.AddSingleton<LogTailService>();
        services.AddSingleton<LogFileQueryService>();
        services.AddSingleton<LoggingService>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLoggingGuide>().Register();

        if (!Option.DisableUILoggingPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UILoggingPage>(
                    UILoggingPage.LOGGING_MONITOR_URL,
                    "Pages:LoggingMonitor:Title",
                    Icons.Material.Filled.Article,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 30));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet("/logging-ui/files",
                async ([FromServices] LoggingService loggingService) =>
                {
                    var result = await loggingService.ListFilesAsync();
                    return result.GetResponse();
                })
                .WithName("列出日志文件")
                .WithTags(tagName)
                .WithSummary("列出日志文件")
                .WithDescription("获取所有可用的日志文件列表");

            endpoints.MapGet("/logging-ui/files/{*filePath}",
                async ([FromRoute] string filePath,
                      [FromServices] LoggingService loggingService) =>
                {
                    filePath = Uri.UnescapeDataString(filePath);
                    var result = await loggingService.OpenFileAsync(filePath);
                    if (result.IsFailed(out var error, out var stream))
                    {
                        return error.GetResponse();
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    var downloadName = Path.GetFileName(filePath);
                    return Results.File(stream, "text/plain", downloadName);
                })
                .WithName("下载日志文件")
                .WithTags(tagName)
                .WithSummary("下载日志文件")
                .WithDescription("下载指定的日志文件");

            endpoints.MapGet("/logging-ui/current/export",
                async ([FromServices] LoggingService loggingService) =>
                {
                    var result = await loggingService.ExportBufferAsync();
                    if (result.IsFailed(out var error, out var export))
                    {
                        return error.GetResponse();
                    }

                    return Results.File(export.Content, export.ContentType, export.FileName);
                })
                .WithName("导出当前日志")
                .WithTags(tagName)
                .WithSummary("导出当前日志")
                .WithDescription("导出当前缓冲区中的日志");
        });
    }
}

/// <summary>
/// Logging UI 模块向导
/// </summary>
public class ModuleLoggingUIGuide : MoModuleGuide<ModuleLoggingUI, ModuleLoggingUIOption, ModuleLoggingUIGuide>
{
}

/// <summary>
/// Logging UI 模块选项
/// </summary>
public class ModuleLoggingUIOption : MoModuleOptionWithMinimalApi<ModuleLoggingUI>
{
    /// <summary>
    /// 是否禁用日志监控页面
    /// </summary>
    public bool DisableUILoggingPage { get; set; }

    /// <summary>
    /// 初始化时获取的日志行数
    /// </summary>
    public int DefaultFetchLines { get; set; } = 500;

    /// <summary>
    /// 临时日志池允许的最大显示行数
    /// </summary>
    public int MaxDisplayLines { get; set; } = 5000;

    /// <summary>
    /// 日志轮询间隔
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 默认启用仅捕获匹配项
    /// </summary>
    public bool DefaultOnlyCapture { get; set; } = true;

    /// <summary>
    /// 预设可选的屏幕日志行数
    /// </summary>
    public IReadOnlyList<int> PresetLineCounts { get; set; } = new[] { 200, 500, 1000, 2500, 5000 };

    /// <summary>
    /// 日志目录优先设定
    /// </summary>
    public string? LogDirectory { get; set; }
}

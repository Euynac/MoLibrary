using System.IO;
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
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.FrameworkUI.UILogging.Models;
using MoLibrary.FrameworkUI.UILogging.Services;
using MoLibrary.Logging.Modules;
using MoLibrary.Tool.MoResponse;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;

/// <summary>
/// LoggingUI 模块构建器扩展
/// </summary>
public static class ModuleLoggingUIBuilderExtensions
{
    public static ModuleLoggingUIGuide ConfigModuleLoggingUI(this WebApplicationBuilder builder,
        Action<ModuleLoggingUIOption>? action = null)
    {
        return new ModuleLoggingUIGuide().Register(action);
    }
}

/// <summary>
/// Logging UI 模块实现
/// </summary>
public class ModuleLoggingUI(ModuleLoggingUIOption option)
    : MoModuleWithDependencies<ModuleLoggingUI, ModuleLoggingUIOption, ModuleLoggingUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.LoggingUI;
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
                .RegisterUIComponents(p => p.RegisterComponent<UILoggingPage>(
                    UILoggingPage.LOGGING_MONITOR_URL,
                    "日志监控",
                    Icons.Material.Filled.Article,
                    "系统管理",
                    addToNav: true,
                    navOrder: 60));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = Option.GetApiGroupName(), Description = "日志监控相关接口" } };

            endpoints.MapGet("/logging-ui/files",
                async ([FromServices] LoggingService loggingService) =>
                {
                    var result = await loggingService.ListFilesAsync();
                    return result.GetResponse();
                })
                .WithName("列出日志文件").WithOpenApi(operation =>
                {
                    operation.Summary = "列出日志文件";
                    operation.Description = "获取所有可用的日志文件列表";
                    operation.Tags = tagGroup;
                    return operation;
                });

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
                .WithName("下载日志文件").WithOpenApi(operation =>
                {
                    operation.Summary = "下载日志文件";
                    operation.Description = "下载指定的日志文件";
                    operation.Tags = tagGroup;
                    return operation;
                });

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
                .WithName("导出当前日志").WithOpenApi(operation =>
                {
                    operation.Summary = "导出当前日志";
                    operation.Description = "导出当前缓冲区中的日志";
                    operation.Tags = tagGroup;
                    return operation;
                });
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
public class ModuleLoggingUIOption : MoModuleControllerOption<ModuleLoggingUI>
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

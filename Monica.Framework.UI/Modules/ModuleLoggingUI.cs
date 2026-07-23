using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.UILogging.Models;
using Monica.Framework.UI.Pages;
using Monica.Core.Results;
using Monica.Framework.UI.UILogging.State;
using Monica.Framework.UI.UILogging.Support;
using Monica.Framework.UI.Localization;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLoggingUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure LoggingUI module
        /// </summary>
        public ModuleLoggingUIGuide AddLoggingUI(Action<ModuleLoggingUIOption>? action = null)
        {
            return builder.AddModule<ModuleLoggingUI, ModuleLoggingUIOption, ModuleLoggingUIGuide>(action);
        }
    }
}

/// <summary>
/// Logging UI module implementation
/// </summary>
[ModuleKey(BuiltInModuleKey.LoggingUI)]
public class ModuleLoggingUI(ModuleLoggingUIOption option)
    : WebModuleBase<ModuleLoggingUI, ModuleLoggingUIOption, ModuleLoggingUIGuide>(option)
{

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
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<LoggingResource>();

        if (!Option.DisablePage)
        {
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedPage<UILoggingMonitorPage, LoggingResource>(
                    UILoggingMonitorPage.PAGE_URL,
                    "Pages:LoggingMonitor:Title",
                    Icons.Material.Filled.Article,
                    BuiltInNavigationCategoryIds.Monitor,
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
/// Logging UI module wizard
/// </summary>
public class ModuleLoggingUIGuide : WebModuleGuide<ModuleLoggingUI, ModuleLoggingUIOption, ModuleLoggingUIGuide>
{
}

/// <summary>
/// Logging UI module options
/// </summary>
public class ModuleLoggingUIOption : MinimalApiModuleOptions<ModuleLoggingUI>
{
    /// <summary>
    /// Whether to disable the log monitoring page
    /// </summary>
    public bool DisablePage { get; set; }

    /// <summary>
    /// Number of log lines obtained during initialization
    /// </summary>
    public int DefaultFetchLines { get; set; } = 500;

    /// <summary>
    /// The maximum number of display lines allowed in the temporary log pool
    /// </summary>
    public int MaxDisplayLines { get; set; } = 5000;

    /// <summary>
    /// Log polling interval
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Capture only matches is enabled by default
    /// </summary>
    public bool DefaultOnlyCapture { get; set; } = true;

    /// <summary>
    /// Default number of optional screen log lines
    /// </summary>
    public IReadOnlyList<int> PresetLineCounts { get; set; } = new[] { 200, 500, 1000, 2500, 5000 };

    /// <summary>
    /// Log directory priority setting
    /// </summary>
    public string? LogDirectory { get; set; }
}

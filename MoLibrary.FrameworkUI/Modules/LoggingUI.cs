using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.FrameworkUI.UILogging.Controllers;
using MoLibrary.FrameworkUI.UILogging.Models;
using MoLibrary.FrameworkUI.UILogging.Services;
using MoLibrary.Logging.Modules;
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
            DependsOnModule<ModuleControllersGuide>().Register()
                .RegisterMoControllers<ModuleLoggingUiController>(Option);

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

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.UI.Components;
using MoLibrary.UI.UICore;
using MudBlazor.Services;

namespace MoLibrary.UI.Modules;

/// <summary>
/// UI模块核心配置扩展方法
/// </summary>
public static class ModuleUICoreBuilderExtensions
{
    /// <summary>
    /// 配置UI核心模块
    /// </summary>
    /// <param name="builder">Web应用构建器</param>
    /// <param name="action">模块配置选项</param>
    /// <returns>UI模块配置引导器</returns>
    public static ModuleUICoreGuide ConfigModuleUICore(this WebApplicationBuilder builder,
        Action<ModuleUICoreOption>? action = null)
    {
        return new ModuleUICoreGuide().Register(action).AddBasicMiddlewares();
    }
}

/// <summary>
/// UI核心模块
/// 提供基于MudBlazor的UI基础设施
/// </summary>
public class ModuleUICore(ModuleUICoreOption option)
    : MoModule<ModuleUICore, ModuleUICoreOption, ModuleUICoreGuide>(option)
{
    /// <summary>
    /// 获取当前模块枚举
    /// </summary>
    /// <returns>UI核心模块枚举</returns>
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.UICore;
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // 添加MudBlazor服务
        services.AddMudServices();

        // 添加Razor组件和交互式服务器组件服务
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // 注册UI组件管理服务
        services.AddSingleton<IUIComponentRegistry, UIComponentRegistry>();
    }
}

/// <summary>
/// UI核心模块配置引导器
/// </summary>
public class ModuleUICoreGuide : MoModuleGuide<ModuleUICore, ModuleUICoreOption, ModuleUICoreGuide>
{


    /// <summary>
    /// 注册UI组件
    /// </summary>
    /// <param name="registrationAction">组件注册配置操作</param>
    /// <returns>配置引导器</returns>
    public ModuleUICoreGuide RegisterUIComponents(Action<IUIComponentRegistry> registrationAction)
    {
        // 在应用程序启动时执行组件注册
        ConfigureApplicationBuilder(builder =>
        {
            var registry = builder.ApplicationBuilder.ApplicationServices.GetRequiredService<IUIComponentRegistry>();
            registrationAction(registry);
        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// 添加UI基础中间件
    /// 注意：这些中间件应该由宿主应用程序调用
    /// </summary>
    /// <returns>配置引导器</returns>
    public ModuleUICoreGuide AddBasicMiddlewares()
    {
        ConfigureApplicationBuilder(builder =>
        {
            var app = builder.ApplicationBuilder;

            //app.UseExceptionHandler("/Error", createScopeForErrors: true);

            //app.MapStaticAssets();  // .NET 9支持

            // 静态文件支持（用于MudBlazor资源）
            app.UseStaticFiles();

            // 防伪令牌
            // Configure your application startup by adding app.UseAntiforgery() in the application startup code. If there are calls to app.UseRouting() and app.UseEndpoints(...), the call to app.UseAntiforgery() must go between them. Calls to app.UseAntiforgery() must be placed after calls to app.UseAuthentication() and app.UseAuthorization()."
            builder.ApplicationBuilder.UseAntiforgery();

        }, EMoModuleApplicationMiddlewaresOrder.AfterUseRouting);


        
        ConfigureEndpoints(builder =>
        {
            builder.WebApplication.MapRazorComponents<MoApp>()
                .AddInteractiveServerRenderMode();
        });

        return this;
    }
}

/// <summary>
/// UI核心模块选项
/// </summary>
public class ModuleUICoreOption : MoModuleOption<ModuleUICore>
{
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Dashboard.Interfaces;
using MoLibrary.Core.Module.Dashboard;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.UI.Components;
using MoLibrary.UI.Components.Pages;
using MudBlazor;
using MudBlazor.Services;
using MoLibrary.UI.UICore.Interfaces;
using MoLibrary.UI.UICore.Services;
using MoLibrary.UI.Services;

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

    public override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsStaging())
        {
            //巨坑：如果不使用下面的语句，WebAssets 在VS中debug环境虽然可以获得css等资源文件，但编译后的debug环境404错误。但生产环境又会访问.nuget目录，导致异常 所以必须限定环境，不能用于生产，生产要通过dotnet publish命令发布。
            //https://github.com/MudBlazor/MudBlazor/issues/2793
            builder.WebHost.UseStaticWebAssets();
            //测试环境可以通过查看.StaticWebAssets.xml看生成的静态资源文件。

            //生产环境是运行dotnet publish，会自动将依赖的static web assets拷贝到wwwroot文件夹。（直接通过dotnet build release 模式是不会生成wwwroot的）
            //https://learn.microsoft.com/en-us/aspnet/core/razor-pages/ui-class?view=aspnetcore-8.0&tabs=visual-stuido#consume-content-from-a-referenced-rcl
        }
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {

        // 注册模块系统状态服务
        if (!Option.DisableModuleSystemUI)
        {
            services.AddSingleton<IModuleSystemStatusService, ModuleSystemStatusService>();
        }

        // 添加MudBlazor服务
        services.AddMudServices();

        // 添加Razor组件和交互式服务器组件服务
        services.AddRazorComponents()
            .AddInteractiveServerComponents(o =>
            {
                o.DetailedErrors = Option.EnableDebug;
            }).AddHubOptions(options =>
            {
                options.EnableDetailedErrors = Option.EnableDebug;
            });

        // 注册UI组件管理服务
        services.AddSingleton<IUIComponentRegistry, UIComponentRegistry>();

        // 注册主题服务
        services.AddSingleton<MoThemeService>();

        // 注册通用Controller调用器
        services.AddScoped(typeof(IUIControllerInvoker<>), typeof(UIControllerInvokerHttpClientProvider<>));
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

            // 静态文件支持（用于MudBlazor资源和Razor类库静态资源）
            app.UseStaticFiles();
            
            //app.UseStaticFiles(new StaticFileOptions()
            //{
            //    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "CustomStyles")),
            //    RequestPath = new PathString("/CustomStyles")
            //});

            // 防伪令牌
            // Configure your application startup by adding app.UseAntiforgery() in the application startup code. If there are calls to app.UseRouting() and app.UseEndpoints(...), the call to app.UseAntiforgery() must go between them. Calls to app.UseAntiforgery() must be placed after calls to app.UseAuthentication() and app.UseAuthorization()."
            builder.ApplicationBuilder.UseAntiforgery();

        }, EMoModuleApplicationMiddlewaresOrder.AfterUseRouting);


        
        ConfigureEndpoints(builder =>
        {
            var registry = builder.ApplicationBuilder.ApplicationServices.GetRequiredService<IUIComponentRegistry>();

            if (!builder.ModuleOption.DisableModuleSystemUI)
            {
                registry.RegisterComponent<ModuleSystemDashboard>(ModuleSystemDashboard.MODULE_SYSTEM_DASHBOARD_URL, "模块系统概览", Icons.Material.Filled.Dashboard, "模块系统", true);
            }

            builder.WebApplication.MapRazorComponents<MoApp>()
                .AddInteractiveServerRenderMode().AddAdditionalAssemblies(registry.GetAdditionalAssemblies());
            //巨坑：如果缺少中间件中的AddAdditionalAssemblies，那么通过F5刷新将会导致404。但通过Router中访问却不会404。
        });

        return this;
    }
}

/// <summary>
/// UI核心模块选项
/// </summary>
public class ModuleUICoreOption : MoModuleOption<ModuleUICore>
{
    /// <summary>
    /// 应用栏名称
    /// </summary>
    public string UIAppBarName { get; set; } = nameof(MoLibrary);

    /// <summary>
    /// 禁用模块系统UI界面
    /// </summary>
    public bool DisableModuleSystemUI { get; set; }

    /// <summary>
    /// 开启Debug模式
    /// </summary>
    public bool EnableDebug { get; set; }

}

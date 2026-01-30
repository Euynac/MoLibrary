using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Module;
using Monica.Core.Module.Dashboard.Interfaces;
using Monica.Core.Module.Dashboard;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.UI.Components;
using Monica.UI.Components.Pages;
using MudBlazor;
using MudBlazor.Services;
using Monica.UI.UICore.Interfaces;
using Monica.UI.UICore.Services;
using Monica.UI.Services;

namespace Monica.UI.Modules;

public static class ModuleUICoreBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 UICore 模块
        /// </summary>
        public static ModuleUICoreGuide AddUICore(Action<ModuleUICoreOption>? action = null)
        {
            return new ModuleUICoreGuide().Register(action).AddBasicMiddlewares();
        }
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
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.UICore;
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
        if(Option.EnableMarkdown)
        {
            services.AddMudMarkdownServices();   
        }

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
        services.AddSingleton<IMoThemeService>(sp => sp.GetRequiredService<MoThemeService>());

        // 注册用户上下文服务
        services.AddScoped<MoUserContextService>();
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
    /// 添加路由重定向规则
    /// </summary>
    /// <param name="fromPath">源路径（例如 "/"）</param>
    /// <param name="toPath">目标路径（例如 "/swagger" 或 "~/swagger"）</param>
    /// <returns>配置引导器</returns>
    public ModuleUICoreGuide AddRouteRedirect(string fromPath, string toPath)
    {
        ConfigureModuleOption(option =>
        {
            option.RouteRedirects[fromPath] = toPath;
        }, secondKey: fromPath);

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
            var app = builder.WebApplication;

            //app.UseExceptionHandler("/Error", createScopeForErrors: true);

            app.MapStaticAssets();  // .NET 9支持

            // // 静态文件支持（用于MudBlazor资源和Razor类库静态资源）
            // app.UseStaticFiles();
            
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
                registry.RegisterComponent<ModuleSystemDashboard>(ModuleSystemDashboard.MODULE_SYSTEM_DASHBOARD_URL, "模块系统概览", Icons.Material.Filled.Dashboard, "模块", true, navOrder: 10);
            }

            // 初始化主题服务
            var themeService = builder.ApplicationBuilder.ApplicationServices.GetRequiredService<MoThemeService>();
            themeService.Initialize();

            // 配置路由重定向
            foreach (var redirect in builder.ModuleOption.RouteRedirects)
            {
                var fromPath = redirect.Key;
                var toPath = redirect.Value;
                builder.WebApplication.MapGet(fromPath, () => Results.LocalRedirect(toPath));
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
    public string UIAppBarName { get; set; } = nameof(Monica);

    /// <summary>
    /// 应用版本号
    /// </summary>
    public string UIAppVersion { get; set; } = "v1.0";

    /// <summary>
    /// 禁用模块系统UI界面
    /// </summary>
    public bool DisableModuleSystemUI { get; set; }

    /// <summary>
    /// 开启Debug模式
    /// </summary>
    public bool EnableDebug { get; set; }

    /// <summary>
    /// 启用Markdown支持
    /// </summary>
    public bool EnableMarkdown { get; set; }

    /// <summary>
    /// 顶部导航栏显示的最大分类数量（超出部分放入"更多"菜单）
    /// </summary>
    public int MaxVisibleCategories { get; set; } = 6;

    /// <summary>
    /// 是否启用导航栏搜索功能
    /// </summary>
    public bool EnableNavBarSearch { get; set; } = true;

    /// <summary>
    /// 路由重定向规则集合，键为源路径，值为目标路径
    /// </summary>
    internal Dictionary<string, string> RouteRedirects { get; set; } = new();

}

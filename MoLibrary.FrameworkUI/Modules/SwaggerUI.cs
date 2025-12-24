using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DomainDrivenDesign.Modules;
using MoLibrary.FrameworkUI.Swagger;

namespace MoLibrary.FrameworkUI.Modules;

/// <summary>
/// ModuleSwaggerUI构建器扩展
/// </summary>
public static class ModuleSwaggerUIBuilderExtensions
{
    public static ModuleSwaggerUIGuide ConfigModuleSwaggerUI(this WebApplicationBuilder builder,
        Action<ModuleSwaggerUIOption>? action = null)
    {
        return new ModuleSwaggerUIGuide().Register(action);
    }
}

/// <summary>
/// Swagger UI增强模块 - 提供自定义导航按钮等UI扩展功能
/// </summary>
public class ModuleSwaggerUI(ModuleSwaggerUIOption option)
    : MoModuleWithDependencies<ModuleSwaggerUI, ModuleSwaggerUIOption, ModuleSwaggerUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SwaggerUI;
    }

    public override void ClaimDependencies()
    {
        // Depend on ModuleSwagger and configure its SwaggerUI extensibility hook
        DependsOnModule<ModuleSwaggerGuide>().Register(swaggerOption =>
        {
            swaggerOption.ExtendSwaggerUIAction = c =>
            {
                // Only inject if there are navigation buttons configured
                if (Option.NavigationButtons.Count == 0)
                    return;

                // Serialize enabled buttons and inject configuration
                var buttonsJson = JsonSerializer.Serialize(
                    Option.NavigationButtons.Where(b => b.Enabled),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                // Inject button configuration via inline script
                c.HeadContent = $$"""

                                  <script>
                                      window.MoSwagger = window.MoSwagger || {};
                                      window.MoSwagger.config = { buttons: {{buttonsJson}} };
                                  </script>
                                  """;
                
                
                // Inject custom JavaScript for navigation buttons
                c.InjectJavascript("../_content/MoLibrary.FrameworkUI/UISwagger/custom.js");

                // Inject custom CSS for navigation buttons
                c.InjectStylesheet("../_content/MoLibrary.FrameworkUI/UISwagger/custom.css");

            };
        });
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // No additional services needed for SwaggerUI module
    }
}

/// <summary>
/// SwaggerUI模块向导
/// </summary>
public class ModuleSwaggerUIGuide : MoModuleGuide<ModuleSwaggerUI, ModuleSwaggerUIOption, ModuleSwaggerUIGuide>
{
    /// <summary>
    /// 添加自定义导航按钮到Swagger UI
    /// </summary>
    /// <param name="name">按钮显示文本</param>
    /// <param name="path">导航路径（例如 "home" 会导航到 "~/home"）</param>
    /// <param name="description">鼠标悬停时显示的工具提示</param>
    /// <param name="order">显示顺序（数字越小越靠前，默认为0）</param>
    public ModuleSwaggerUIGuide AddNavigationButton(
        string name,
        string path,
        string? description = null,
        int order = 0)
    {
        ConfigureModuleOption(option =>
        {
            option.NavigationButtons.Add(new SwaggerNavigationButton
            {
                Name = name,
                Path = path,
                Description = description,
                Order = order
            });
        }, secondKey: name);

        return this;
    }
}

/// <summary>
/// SwaggerUI模块选项
/// </summary>
public class ModuleSwaggerUIOption : MoModuleOption<ModuleSwaggerUI>
{
    /// <summary>
    /// 自定义导航按钮，显示在Swagger UI顶部工具栏的右侧。
    /// </summary>
    public List<SwaggerNavigationButton> NavigationButtons { get; set; } = new();
}

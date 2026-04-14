using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.UI.Pages;
using Monica.Framework.UI.UISwagger.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSwaggerUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the SwaggerUI module
        /// </summary>
        public static ModuleSwaggerUIGuide AddSwaggerUI(Action<ModuleSwaggerUIOption>? action = null)
        {
            return new ModuleSwaggerUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Swagger UI enhancement module - provides UI extension functions such as custom navigation buttons
/// </summary>
[ModuleKey(BuiltInModuleKey.SwaggerUI)]
public class ModuleSwaggerUI(ModuleSwaggerUIOption option)
    : ModuleBase<ModuleSwaggerUI, ModuleSwaggerUIOption, ModuleSwaggerUIGuide>(option)
{

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
                c.InjectJavascript("../_content/Monica.Framework.UI/UISwagger/custom.js");

                // Inject custom CSS for navigation buttons
                c.InjectStylesheet("../_content/Monica.Framework.UI/UISwagger/custom.css");

            };
        });
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // No additional services needed for SwaggerUI module
    }
}

/// <summary>
/// SwaggerUI module wizard
/// </summary>
public class ModuleSwaggerUIGuide : ModuleGuide<ModuleSwaggerUI, ModuleSwaggerUIOption, ModuleSwaggerUIGuide>
{
    /// <summary>
    /// Add custom navigation buttons to Swagger UI
    /// </summary>
    /// <param name="name">Button display text</param>
    /// <param name="path">Navigation path (for example, <see cref="UISystemInfoPage.PAGE_URL"/> navigates to "~/system-info").</param>
    /// <param name="description">Tooltip displayed on mouseover</param>
    /// <param name="order">Display order (the smaller the number, the higher it is, the default is 0)</param>
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
/// SwaggerUI module options
/// </summary>
public class ModuleSwaggerUIOption : ModuleOptions<ModuleSwaggerUI>
{
    /// <summary>
    /// Custom navigation buttons that appear on the right side of the Swagger UI top toolbar.
    /// </summary>
    public List<SwaggerNavigationButton> NavigationButtons { get; set; } = new();
}

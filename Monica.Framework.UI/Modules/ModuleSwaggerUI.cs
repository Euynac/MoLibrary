using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.UI.Pages;
using Monica.Framework.UI.UISwagger.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSwaggerUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the SwaggerUI module
        /// </summary>
        public ModuleRegistration<ModuleSwaggerUI, ModuleSwaggerUIOption> AddSwaggerUI(
            Action<ModuleSwaggerUIOption>? action = null)
        {
            return builder.AddModule<ModuleSwaggerUI, ModuleSwaggerUIOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleSwaggerUI, ModuleSwaggerUIOption> registration)
    {
        /// <summary>
        /// Redirects the application root route to the Swagger UI page.
        /// </summary>
        /// <returns>The same host-bound registration.</returns>
        public ModuleRegistration<ModuleSwaggerUI, ModuleSwaggerUIOption> SetAsRootRoute()
        {
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .AddRouteRedirect("/", "/swagger");
            return registration;
        }

        /// <summary>
        /// Adds a custom navigation button to Swagger UI.
        /// </summary>
        public ModuleRegistration<ModuleSwaggerUI, ModuleSwaggerUIOption> AddNavigationButton(
            string name,
            string path,
            string? description = null,
            int order = 0)
        {
            return registration.Configure(option => option.NavigationButtons.Add(new SwaggerNavigationButton
            {
                Name = name,
                Path = path,
                Description = description,
                Order = order
            }));
        }
    }
}

/// <summary>
/// Swagger UI enhancement module - provides UI extension functions such as custom navigation buttons
/// </summary>
public class ModuleSwaggerUI : MonicaModule<ModuleSwaggerUIOption>, IUIModule
{
    private IReadOnlyList<SwaggerNavigationButton> _navigationButtons = [];

    public override void Describe(ModuleDescriptor module)
    {
        // Depend on ModuleSwagger and configure its SwaggerUI extensibility hook
        module.Require<ModuleSwagger, ModuleSwaggerOption>(swaggerOption =>
        {
            var existingAction = swaggerOption.ExtendSwaggerUIAction;

            swaggerOption.ExtendSwaggerUIAction = c =>
            {
                existingAction?.Invoke(c);

                // Only inject if there are navigation buttons configured
                if (_navigationButtons.Count == 0)
                {
                    return;
                }

                // Serialize enabled buttons and inject configuration
                var buttonsJson = JsonSerializer.Serialize(
                    _navigationButtons,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                // Append button configuration without clobbering any existing head injections.
                c.HeadContent += $$"""

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

    public override void ConfigureServices(ModuleContext<ModuleSwaggerUIOption> context)
    {
        _navigationButtons = Option.NavigationButtons
            .Where(static button => button.Enabled)
            .OrderBy(static button => button.Order)
            .ToArray();
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.WebApi.Swagger.Models;
using Monica.WebApi.Swagger.Services.Support;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(BuiltInModuleKey.Swagger)]
public class ModuleSwagger(ModuleSwaggerOption option) : WebModuleBase<ModuleSwagger, ModuleSwaggerOption, ModuleSwaggerGuide>(option)
{
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        var documentCatalog = new SwaggerDocumentCatalog(Option);

        app.UseSwagger();
        app.UseSwaggerUI(swaggerUiOptions =>
        {
            SwaggerUIOptionConfigurator.Configure(swaggerUiOptions, Option, documentCatalog);
        });
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        var documentCatalog = new SwaggerDocumentCatalog(Option);

        services.AddSwaggerGen(swaggerGenOptions =>
        {
            SwaggerGenOptionConfigurator.Configure(swaggerGenOptions, Option, Logger, documentCatalog);
        });
    }
}

public static class ModuleSwaggerBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers and configures the Swagger module.
        /// </summary>
        public static ModuleSwaggerGuide AddSwagger(Action<ModuleSwaggerOption>? action = null)
        {
            return new ModuleSwaggerGuide().Register(action);
        }
    }
}

public class ModuleSwaggerGuide : WebModuleGuide<ModuleSwagger, ModuleSwaggerOption, ModuleSwaggerGuide>
{
}

public class ModuleSwaggerOption : ModuleOptions<ModuleSwagger>
{
    /// <summary>
    /// Callback that can extend Swagger generation after the Monica defaults have been applied.
    /// </summary>
    public Action<SwaggerGenOptions>? ExtendSwaggerGenAction { get; set; }

    /// <summary>
    /// Callback to extend Swagger UI configuration, allowing other modules (such as the SwaggerUI module) to customize its behavior.
    /// </summary>
    public Action<SwaggerUIOptions>? ExtendSwaggerUIAction { get; set; }

    /// <summary>
    /// Application name.
    /// </summary>
    public string? AppName { get; set; } = "ApplicationName";

    /// <summary>
    /// API version.
    /// </summary>
    public string? Version { get; set; } = "v1";

    /// <summary>
    /// Swagger document name used for Monica framework endpoints.
    /// </summary>
    public string MonicaDocumentName { get; set; } = "monica";

    /// <summary>
    /// Display title used for the Monica Swagger definition.
    /// </summary>
    public string MonicaDocumentTitle { get; set; } = "Monica API";

    /// <summary>
    /// Swagger document name used for business or application endpoints.
    /// </summary>
    public string BusinessDocumentName { get; set; } = "business";

    /// <summary>
    /// Display title used for the business Swagger definition.
    /// </summary>
    public string BusinessDocumentTitle { get; set; } = "Business API";

    /// <summary>
    /// Resolves the Swagger document kind for an endpoint. Return <see langword="null"/> to use the default Monica endpoint marker rule.
    /// </summary>
    public Func<ApiDescription, ESwaggerDocumentKind?>? DocumentKindResolver { get; set; }

    /// <summary>
    /// Document description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Disable Swagger's automatic XML documentation loading.
    /// </summary>
    public bool DisableXmlDocumentation { get; set; }

    /// <summary>
    /// Names of the service projects whose XML documentation should be picked up for Swagger; each project must set `<GenerateDocumentationFile>True</GenerateDocumentationFile>`.
    /// </summary>
    public string[]? DocumentAssemblies { get; set; }

    /// <summary>
    /// Disable automatically adding module-system-related assemblies to the Swagger generation pipeline.
    /// </summary>
    public bool DisableAutoIncludeModuleSystemRelatedAsDocumentAssembly { get; set; }

    /// <summary>
    /// Require authentication for Swagger UI.
    /// </summary>
    public bool UseAuth { get; set; }

    /// <summary>
    /// Disable the inheritdoc filter that imports XML comments from referenced methods.
    /// </summary>
    public bool DisableInheritDocFilter { get; set; }

    /// <summary>
    /// Swagger UI route prefix (defaults to "swagger"); set to an empty string to expose Swagger at the application root.
    /// </summary>
    public string RoutePrefix { get; set; } = "swagger";
}

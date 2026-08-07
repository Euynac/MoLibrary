using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.WebApi.Swagger.Models;
using Monica.WebApi.Swagger.Services.Support;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public class ModuleSwagger : MonicaModule<ModuleSwaggerOption>, IWebHostRequiredModule
{
    private SwaggerDocumentCatalog? _documentCatalog;

    public override void ConfigureApplicationBuilder(WebModuleContext<ModuleSwaggerOption> context)
    {
        context.ApplicationBuilder.UseSwagger();
        context.ApplicationBuilder.UseSwaggerUI(swaggerUiOptions =>
        {
            SwaggerUIOptionConfigurator.Configure(
                swaggerUiOptions,
                Option,
                GetDocumentCatalog(),
                Application.Application);
        });
    }

    public override void ConfigureServices(ModuleContext<ModuleSwaggerOption> context)
    {
        context.Services.AddSwaggerGen(swaggerGenOptions =>
        {
            SwaggerGenOptionConfigurator.Configure(
                swaggerGenOptions,
                Option,
                Logger,
                GetDocumentCatalog(),
                Application.TypeFinder);
        });
    }

    private SwaggerDocumentCatalog GetDocumentCatalog()
    {
        return _documentCatalog ??= new SwaggerDocumentCatalog(Option, Application.Application, Logger);
    }
}

public static class ModuleSwaggerBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers and configures the Swagger module.
        /// </summary>
        public ModuleRegistration<ModuleSwagger, ModuleSwaggerOption> AddSwagger(
            Action<ModuleSwaggerOption>? action = null)
        {
            return builder.AddModule<ModuleSwagger, ModuleSwaggerOption>(action);
        }
    }
}

public class ModuleSwaggerOption : ModuleOptions<ModuleSwagger>
{
    /// <summary>
    /// Callback that can extend Swagger generation after Monica's document defaults have been applied.
    /// </summary>
    public Action<SwaggerGenOptions>? ExtendSwaggerGenAction { get; set; }

    /// <summary>
    /// Callback that can extend Swagger UI configuration after Monica has registered the default document endpoints.
    /// </summary>
    public Action<SwaggerUIOptions>? ExtendSwaggerUIAction { get; set; }

    /// <summary>
    /// Application name used by the Swagger UI title and the default business document title.
    /// When not configured, Swagger uses the application defaults configured through <see cref="IMonicaBuilder.ConfigureApplication"/>.
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// OpenAPI version stamped onto every registered Swagger document. Defaults to <c>v1</c>.
    /// This API version is intentionally independent from application release or display versions.
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Route segment of the primary business document. When not set, the Swagger module falls back to <see cref="ApiVersion"/>.
    /// </summary>
    public string? BusinessDocumentName { get; set; }

    /// <summary>
    /// Display title of the primary business document. When not set, the Swagger module uses the resolved application name.
    /// </summary>
    public string? BusinessDocumentTitle { get; set; }

    /// <summary>
    /// Top-level OpenAPI description of the primary business document.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Monica framework document settings. Set <see cref="MonicaDocumentSettings.Enabled"/> to <see langword="false"/> to suppress it entirely.
    /// </summary>
    public MonicaDocumentSettings Monica { get; set; } = new();

    /// <summary>
    /// Additional Swagger documents registered beyond the primary business and Monica framework documents.
    /// </summary>
    public IList<SwaggerDocumentDescriptor> AdditionalDocuments { get; } = new List<SwaggerDocumentDescriptor>();

    /// <summary>
    /// Optional endpoint-to-document override. Return a registered document name to force the document, or <see langword="null"/> to fall through to the default resolution chain.
    /// </summary>
    public Func<ApiDescription, string?>? DocumentNameResolver { get; set; }

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

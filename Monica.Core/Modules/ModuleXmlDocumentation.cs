using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Core.XmlDocumentation.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleXmlDocumentationBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the XML documentation module.
        /// </summary>
        public ModuleXmlDocumentationGuide AddXmlDocumentation(Action<ModuleXmlDocumentationOption>? action = null)
        {
            return builder.AddModule<ModuleXmlDocumentation, ModuleXmlDocumentationOption, ModuleXmlDocumentationGuide>(action);
        }
    }
}

/// <summary>
/// Provides XML documentation lookup and cache inspection endpoints.
/// </summary>
[ModuleKey(BuiltInModuleKey.XmlDocumentation)]
public class ModuleXmlDocumentation(ModuleXmlDocumentationOption option)
    : WebModuleBase<ModuleXmlDocumentation, ModuleXmlDocumentationOption, ModuleXmlDocumentationGuide>(option)
{
    /// <summary>
    /// Configures services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IXmlDocumentationService, XmlDocumentationService>();
    }

    /// <summary>
    /// Configures endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (!option.EnableEndpoints)
        {
            return;
        }

        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            // Return cached XML documentation details.
            endpoints.MapGet("/xml-docs/cache", ([FromServices] IXmlDocumentationService xmlService) =>
            {
                try
                {
                    var cachedDocs = xmlService.GetCachedDocuments();
                    return Res.Ok(cachedDocs.Select(doc => new
                    {
                        doc.AssemblyName,
                        doc.XmlFilePath,
                        doc.CachedAt,
                        HasDocument = doc.Document != null
                    })).GetResponse();
                }
                catch (Exception ex)
                {
                    return Res.Fail($"Failed to get the XML documentation cache: {ex.Message}").GetResponse();
                }
            })
            .WithName("GetXmlDocumentationCache")
            .WithTags(tagName)
            .WithSummary("Gets the XML documentation cache")
            .WithDescription("Returns the assembly, file path, and cache state for each loaded XML documentation file.");

            // Clear the cached XML documentation.
            endpoints.MapPost("/xml-docs/cache/clear", ([FromServices] IXmlDocumentationService xmlService) =>
            {
                try
                {
                    xmlService.ClearCache();
                    return Res.Ok("The XML documentation cache was cleared.").GetResponse();
                }
                catch (Exception ex)
                {
                    return Res.Fail($"Failed to clear the XML documentation cache: {ex.Message}").GetResponse();
                }
            })
            .WithName("ClearXmlDocumentationCache")
            .WithTags(tagName)
            .WithSummary("Clears the XML documentation cache")
            .WithDescription("Evicts all cached XML documentation files from this Monica host.");
        });
    }
}

/// <summary>
/// Provides fluent configuration for the XML documentation module.
/// </summary>
public class ModuleXmlDocumentationGuide : WebModuleGuide<ModuleXmlDocumentation, ModuleXmlDocumentationOption, ModuleXmlDocumentationGuide>
{
}

/// <summary>
/// Configures XML documentation inspection for one Monica host.
/// </summary>
public class ModuleXmlDocumentationOption : MinimalApiModuleOptions<ModuleXmlDocumentation>
{
    /// <summary>
    /// Enables the XML documentation cache endpoints exposed by this module.
    /// Leave this enabled when you need runtime inspection, or disable it to avoid exposing diagnostic endpoints.
    /// </summary>
    public bool EnableEndpoints { get; set; } = true;
}

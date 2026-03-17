using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Features.MoXmlDocumentation;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Tool.MoResponse;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleXmlDocumentationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the XmlDocumentation module.
        /// </summary>
        public static ModuleXmlDocumentationGuide AddXmlDocumentation(Action<ModuleXmlDocumentationOption>? action = null)
        {
            return new ModuleXmlDocumentationGuide().Register(action);
        }
    }
}

public class ModuleXmlDocumentation(ModuleXmlDocumentationOption option)
    : MoModule<ModuleXmlDocumentation, ModuleXmlDocumentationOption, ModuleXmlDocumentationGuide>(option)
{
    public static IXmlDocumentationService? Singleton { get; private set; }
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.XmlDocumentation;
    }

    /// <summary>
    /// Configures services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        Singleton = new XmlDocumentationService();
        // Register the XML documentation service as a singleton.
        services.AddSingleton<IXmlDocumentationService, XmlDocumentationService>(_ =>
            (XmlDocumentationService) Singleton);
    }

    /// <summary>
    /// Configures endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
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
                    return Res.Fail($"获取XML文档缓存信息失败: {ex.Message}").GetResponse();
                }
            })
            .WithName("获取XML文档缓存信息")
            .WithTags(tagName)
            .WithSummary("获取XML文档缓存信息")
            .WithDescription("获取当前缓存的所有XML文档信息，包括程序集名称、文件路径等");

            // Clear the cached XML documentation.
            endpoints.MapPost("/xml-docs/cache/clear", ([FromServices] IXmlDocumentationService xmlService) =>
            {
                try
                {
                    xmlService.ClearCache();
                    return Res.Ok("XML文档缓存已清空").GetResponse();
                }
                catch (Exception ex)
                {
                    return Res.Fail($"清空XML文档缓存失败: {ex.Message}").GetResponse();
                }
            })
            .WithName("清空XML文档缓存")
            .WithTags(tagName)
            .WithSummary("清空XML文档缓存")
            .WithDescription("清空所有缓存的XML文档，释放内存");
        });
    }
}

public class ModuleXmlDocumentationGuide : MoModuleGuide<ModuleXmlDocumentation, ModuleXmlDocumentationOption, ModuleXmlDocumentationGuide>
{
}

public class ModuleXmlDocumentationOption : MoModuleOptionWithMinimalApi<ModuleXmlDocumentation>
{
    /// <summary>
    /// Enables endpoints. Enabled by default.
    /// </summary>
    public bool EnableEndpoints { get; set; } = true;
}

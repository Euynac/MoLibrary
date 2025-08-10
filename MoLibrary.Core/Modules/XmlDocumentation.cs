using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features.MoXmlDocumentation;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Modules;

public static class ModuleXmlDocumentationBuilderExtensions
{
    public static ModuleXmlDocumentationGuide ConfigModuleXmlDocumentation(this WebApplicationBuilder builder,
        Action<ModuleXmlDocumentationOption>? action = null)
    {
        return new ModuleXmlDocumentationGuide().Register(action);
    }
}

public class ModuleXmlDocumentation(ModuleXmlDocumentationOption option)
    : MoModule<ModuleXmlDocumentation, ModuleXmlDocumentationOption, ModuleXmlDocumentationGuide>(option)
{
    public static IXmlDocumentationService? Singleton { get; private set; }
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.XmlDocumentation;
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        Singleton = new XmlDocumentationService();
        // 注册XML文档服务为单例
        services.AddSingleton<IXmlDocumentationService, XmlDocumentationService>(_ =>
            (XmlDocumentationService) Singleton);
    }

    /// <summary>
    /// 配置端点
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (!Option.EnableEndpoints) return;

        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = Option.GetApiGroupName(), Description = "XML文档服务接口" } };

            // 获取缓存的XML文档信息
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
            }).WithName("获取XML文档缓存信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取XML文档缓存信息";
                operation.Description = "获取当前缓存的所有XML文档信息，包括程序集名称、文件路径等";
                operation.Tags = tagGroup;
                return operation;
            });

            // 清空缓存
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
            }).WithName("清空XML文档缓存").WithOpenApi(operation =>
            {
                operation.Summary = "清空XML文档缓存";
                operation.Description = "清空所有缓存的XML文档，释放内存";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}

public class ModuleXmlDocumentationGuide : MoModuleGuide<ModuleXmlDocumentation, ModuleXmlDocumentationOption, ModuleXmlDocumentationGuide>
{
}

public class ModuleXmlDocumentationOption : MoModuleControllerOption<ModuleXmlDocumentation>
{
    /// <summary>
    /// 是否启用端点（默认禁用）
    /// </summary>
    public bool EnableEndpoints { get; set; } = true;
}
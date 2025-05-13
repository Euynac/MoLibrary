using MoLibrary.Core.Module.Interfaces;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MoLibrary.DomainDrivenDesign.Modules;

public class ModuleSwaggerOption : IMoModuleOption<ModuleSwagger>
{
    public Action<SwaggerGenOptions>? ExtendSwaggerGenAction { get; set; }

    /// <summary>
    /// 应用名
    /// </summary>
    public string? AppName { get; set; } = "ApplicationName";

    /// <summary>
    /// 接口版本
    /// </summary>
    public string? Version { get; set; } = "v1";

    /// <summary>
    /// 文档描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 服务项目名，用于Swagger文档生成。注意需要设置项目&lt;GenerateDocumentationFile&gt;True&lt;/GenerateDocumentationFile&gt; XML文档用于生成注释
    /// </summary>
    public string[]? DocumentAssemblies { get; set; }

    /// <summary>
    /// 是否禁用自动包含入口程序集作为文档生成
    /// </summary>
    public bool DisableAutoIncludeEntryAsDocumentAssembly { get; set; }

    /// <summary>
    /// 是否使用认证
    /// </summary>
    public bool UseAuth { get; set; }

}
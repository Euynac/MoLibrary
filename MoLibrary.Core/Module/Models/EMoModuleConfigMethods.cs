namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 模块构建顺序
/// </summary>
public enum EMoModuleConfigMethods
{
    ClaimDependencies,
    ConfigureBuilder,
    ConfigureServices,
    IterateBusinessTypes,
    PostConfigureServices,
    ConfigureApplicationBuilder,
    ConfigureEndpoints
}
namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 模块构建顺序
/// </summary>
public enum EMoModuleConfigMethods
{
    ConfigureBuilder,
    ConfigureServices,
    PostConfigureServices,
    ConfigureApplicationBuilder,
    ConfigureEndpoints
}
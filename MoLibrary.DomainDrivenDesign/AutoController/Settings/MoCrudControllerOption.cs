using MoLibrary.Core.Module.Interfaces;
using MoLibrary.DomainDrivenDesign.Modules;

namespace MoLibrary.DomainDrivenDesign.AutoController.Settings;

public class MoCrudControllerOption : IMoModuleExtraOption<ModuleAutoControllers>
{
    /// <summary>
    /// 自动CRUD路径前缀
    /// </summary>
    public string RoutePath { get; set; } = "api/v1/[controller]";
    /// <summary>
    /// Controller自动注册后缀
    /// </summary>
    public string CrudControllerPostfix { get; set; } = "CrudService";
}
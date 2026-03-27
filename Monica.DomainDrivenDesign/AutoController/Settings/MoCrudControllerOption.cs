using Monica.Core.Modularity.Interfaces;
using Monica.Modules;

namespace Monica.DomainDrivenDesign.AutoController.Settings;

public class MoCrudControllerOption : IMoModuleExtraOption<ModuleAutoControllers>
{
    /// <summary>
    /// Route prefix used for auto-generated CRUD endpoints.
    /// </summary>
    public string RoutePath { get; set; } = "api/v1/[controller]";
    /// <summary>
    /// Required class-name suffix for automatic controller registration.
    /// </summary>
    public string CrudControllerPostfix { get; set; } = "CrudService";
}

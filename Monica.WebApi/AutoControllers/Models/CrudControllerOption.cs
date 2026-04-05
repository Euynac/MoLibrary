using Monica.Core.Modularity.Abstractions;
using Monica.Modules;

namespace Monica.WebApi.AutoControllers.Models;

public class CrudControllerOption : IModuleExtraOptions<ModuleAutoControllers>
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

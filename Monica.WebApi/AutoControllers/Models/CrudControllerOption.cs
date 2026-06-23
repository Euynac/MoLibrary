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
    /// Class-name suffix stripped from a CRUD application service when deriving its controller (route) name.
    /// </summary>
    /// <remarks>
    /// This is purely a route-naming aid: a service such as <c>AircraftAppService</c> becomes the controller
    /// <c>Aircraft</c> and the route segment <c>aircraft</c>. It does not gate controller registration (that is decided
    /// by the <c>ICrudApplicationService</c> marker interface) and is not a naming-convention rule; name-convention
    /// enforcement lives in the project-unit system (Monica.Framework). If a service name does not end with this
    /// suffix, the name is simply left unchanged.
    /// </remarks>
    public string CrudControllerPostfix { get; set; } = "CrudService";
}

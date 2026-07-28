using Monica.ProjectUnits.Services.Support;
using Monica.Tool.Extensions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.AutoControllers;
using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// CRUD application services that participate in automatic controller generation (implement
/// <see cref="ICrudApplicationService"/>).
/// </summary>
/// <remarks>
/// These services also derive from <see cref="ApplicationService"/>, but they are modelled as their own project-unit
/// type so their naming convention is owned by the project-unit system rather than the web layer. The web layer
/// (Monica.WebApi) holds no naming rule for them; configure the convention through
/// <c>ModuleProjectUnitsOption.ConventionOptions.Dict[EProjectUnitType.CrudApplicationService]</c>.
/// </remarks>
public class UnitCrudApplicationService : ProjectUnit
{
    internal UnitCrudApplicationService(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.CrudApplicationService, catalog, MvcExecutionPoints.Action)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass
               && Type.IsSubclassOf(typeof(ApplicationService))
               && Type.IsImplementInterface<ICrudApplicationService>();
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitCrudApplicationService(type, catalog);
        unit = unit.VerifyType() ? unit : null;
        unit?.InitializeMethods<ApplicationService>();
        return unit;
    }
}

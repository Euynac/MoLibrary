using Monica.Framework.ProjectUnits.Abstractions;
using Monica.Framework.ProjectUnits.Services.Support;
using Monica.Tool.Extensions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.Framework.ProjectUnits.Models;

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
/// <param name="type"></param>
public class UnitCrudApplicationService(Type type)
    : ProjectUnit(type, EProjectUnitType.CrudApplicationService), IHasProjectUnitFactory
{
    static UnitCrudApplicationService()
    {
        AddUnitRegisterFactory(Factory);
    }

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass
               && Type.IsSubclassOf(typeof(ApplicationService))
               && Type.IsImplementInterface<ICrudApplicationService>();
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var unit = new UnitCrudApplicationService(context.Type);
        unit = unit.VerifyType() ? unit : null;
        unit?.InitializeMethods<ApplicationService>();
        return unit;
    }

    public override void DoingConnect()
    {
        // Detecting unit dependencies in constructors (e.g. repositories).
        DetectConstructorUnitDependencies();
    }
}

using Monica.Framework.ProjectUnits.Abstractions;
using Monica.Modules;
using Monica.WebApi.Abstractions;

namespace Monica.Framework.ProjectUnits.Models;

/// <summary>
/// Domain services
/// </summary>
/// <param name="type"></param>
public class UnitDomainService(Type type) : ProjectUnit(type, EProjectUnitType.DomainService), IHasProjectUnitFactory
{
    protected override bool ShouldAnalyzeConstructorDependencies => true;

    static UnitDomainService()
    {
        AddUnitRegisterFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(DomainService));
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Domain"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var unit = new UnitDomainService(context.Type);
        if (!unit.VerifyType()) return null;
        
        // Initialization method metadata
        unit.InitializeMethods<DomainService>();
        return unit;
    }
}

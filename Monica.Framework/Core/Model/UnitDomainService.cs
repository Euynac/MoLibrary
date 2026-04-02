using Monica.Framework.Core.Interfaces;
using Monica.Modules;
using Monica.WebApi.Abstractions;

namespace Monica.Framework.Core.Model;

/// <summary>
/// Domain services
/// </summary>
/// <param name="type"></param>
public class UnitDomainService(Type type) : ProjectUnit(type, EProjectUnitType.DomainService), IHasProjectUnitFactory
{
    static UnitDomainService()
    {
        AddUnitRegisterFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(DomainService));
    }

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
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
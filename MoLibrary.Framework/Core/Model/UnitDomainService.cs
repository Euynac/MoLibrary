using MoLibrary.DomainDrivenDesign;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 领域服务
/// </summary>
/// <param name="type"></param>
public class UnitDomainService(Type type) : ProjectUnit(type, EProjectUnitType.DomainService), IHasProjectUnitFactory
{
    static UnitDomainService()
    {
        AddFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(MoDomainService));
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
        return unit.VerifyType() ? unit : null;
    }
}
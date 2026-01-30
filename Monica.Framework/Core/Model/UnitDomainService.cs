using Monica.DomainDrivenDesign;
using Monica.Framework.Core.Interfaces;
using Monica.Framework.Modules;

namespace Monica.Framework.Core.Model;

/// <summary>
/// 领域服务
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
        if (!unit.VerifyType()) return null;
        
        // 初始化方法元数据
        unit.InitializeMethods<MoDomainService>();
        return unit;
    }
}
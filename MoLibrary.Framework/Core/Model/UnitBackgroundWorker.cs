using MoLibrary.BackgroundJob.Hangfire.Workers;
using MoLibrary.Framework.Modules;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 后台定时作业
/// </summary>
/// <param name="type"></param>
public class UnitBackgroundWorker(Type type) : ProjectUnit(type, EProjectUnitType.BackgroundWorker), IHasProjectUnitFactory
{
    static UnitBackgroundWorker()
    {
        AddFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(MoHangfireBackgroundWorker));
    }

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            Prefix = "Worker"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var unit = new UnitBackgroundWorker(context.Type);
        return unit.VerifyType() ? unit : null;
    }
}
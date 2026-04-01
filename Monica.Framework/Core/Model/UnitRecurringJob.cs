using Monica.Framework.Core.Interfaces;
using Monica.JobScheduler.Abstractions;
using Monica.Modules;

namespace Monica.Framework.Core.Model;

/// <summary>
/// Background scheduled jobs
/// </summary>
/// <param name="type"></param>
public class UnitRecurringJob(Type type) : ProjectUnit(type, EProjectUnitType.RecurringJob), IHasProjectUnitFactory
{
    static UnitRecurringJob()
    {
        AddUnitRegisterFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(RecurringJob));
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
        var unit = new UnitRecurringJob(context.Type);
        return unit.VerifyType() ? unit : null;
    }
}
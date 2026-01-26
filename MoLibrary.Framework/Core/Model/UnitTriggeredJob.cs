using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Framework.Modules;
using MoLibrary.JobScheduler.Jobs;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 后台触发式任务
/// </summary>
/// <param name="type"></param>
public class UnitTriggeredJob(Type type) : ProjectUnit(type, EProjectUnitType.TriggeredJob), IHasProjectUnitFactory
{
    public Type? JobArgsType { get; set; }

    static UnitTriggeredJob()
    {
        AddUnitRegisterFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOfRawGeneric(typeof(MoTriggeredJob<>));
    }

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            Prefix = "Job"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var type = context.Type;
        var unit = new UnitTriggeredJob(type);
        if (!type.IsClass || !type.IsSubclassOfRawGeneric(typeof(MoTriggeredJob<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        unit.JobArgsType = genericType.GetGenericArguments().First();
        return unit;
    }
}
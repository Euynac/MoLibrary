using Monica.Framework.Core.Interfaces;
using Monica.JobScheduler.Abstractions;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core.Model;

/// <summary>
/// Background triggered tasks
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
        return Type.IsClass && Type.IsSubclassOfRawGeneric(typeof(TriggeredJob<>));
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
        if (!type.IsClass || !type.IsSubclassOfRawGeneric(typeof(TriggeredJob<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        unit.JobArgsType = genericType.GetGenericArguments().First();
        return unit;
    }
}
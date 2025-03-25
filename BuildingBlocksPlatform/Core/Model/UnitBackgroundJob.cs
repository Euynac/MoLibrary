using BuildingBlocksPlatform.SeedWork;

namespace BuildingBlocksPlatform.Core.Model;

/// <summary>
/// 后台任务
/// </summary>
/// <param name="type"></param>
public class UnitBackgroundJob(Type type) : ProjectUnit(type, EProjectUnitType.BackgroundJob), IHasProjectUnitFactory
{
    public Type? JobArgsType { get; set; }

    static UnitBackgroundJob()
    {
        AddFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOfRawGeneric(typeof(OurBackgroundJob<>));
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
        var unit = new UnitBackgroundJob(type);
        if (Option.ConventionOptions.EnablePerformanceMode && !unit.VerifyNameConvention()) return null;
        if (!type.IsClass || !type.IsSubclassOfRawGeneric(typeof(OurBackgroundJob<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        unit.JobArgsType = genericType.GetGenericArguments().First();
        return unit;
    }
}
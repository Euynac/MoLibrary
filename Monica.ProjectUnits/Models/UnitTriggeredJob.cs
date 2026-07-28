using Monica.ProjectUnits.Services.Support;
using Monica.JobScheduler;
using Monica.JobScheduler.Abstractions;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Background triggered tasks
/// </summary>
public class UnitTriggeredJob : ProjectUnit
{
    internal UnitTriggeredJob(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.TriggeredJob, catalog, JobSchedulerExecutionPoints.TriggeredAttempt)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    /// <summary>
    /// Gets or sets the argument type accepted by the triggered job.
    /// </summary>
    public Type? JobArgsType { get; set; }

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOfRawGeneric(typeof(TriggeredJob<>));
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Job"
        };
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitTriggeredJob(type, catalog);
        if (!type.IsClass || !type.IsSubclassOfRawGeneric(typeof(TriggeredJob<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        unit.JobArgsType = genericType.GetGenericArguments().First();
        return unit;
    }
}

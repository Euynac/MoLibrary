using Monica.ProjectUnits.Services.Support;
using Monica.JobScheduler;
using Monica.JobScheduler.Abstractions;
using Monica.Modules;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Background scheduled jobs
/// </summary>
public class UnitRecurringJob : ProjectUnit
{
    internal UnitRecurringJob(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.RecurringJob, catalog, JobSchedulerExecutionPoints.RecurringAttempt)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(RecurringJob));
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Worker"
        };
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitRecurringJob(type, catalog);
        return unit.VerifyType() ? unit : null;
    }
}

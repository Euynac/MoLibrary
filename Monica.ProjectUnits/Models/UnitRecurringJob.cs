using Monica.Core.TypeDiscovery.Models;
using Monica.Modules;
using Monica.ProjectUnits.Services.Support;
using Monica.JobScheduler;
using Monica.JobScheduler.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Background scheduled jobs
/// </summary>
public class UnitRecurringJob : ProjectUnit
{
    internal UnitRecurringJob(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.RecurringJob, catalog, JobSchedulerExecutionPoints.RecurringAttempt)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;
    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Worker"
        };
    }

    internal static ProjectUnit Create(BusinessTypeShape shape, ProjectUnitCatalog catalog)
    {
        var unit = new UnitRecurringJob(shape, catalog);
        unit.CheckNameConventionMode();
        return unit;
    }
}

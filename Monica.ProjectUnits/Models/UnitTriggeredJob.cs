using Monica.Core.TypeDiscovery.Models;
using Monica.Modules;
using Monica.ProjectUnits.Services.Support;
using Monica.JobScheduler;
using Monica.JobScheduler.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Background triggered tasks
/// </summary>
public class UnitTriggeredJob : ProjectUnit
{
    internal UnitTriggeredJob(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.TriggeredJob, catalog, JobSchedulerExecutionPoints.TriggeredAttempt)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    /// <summary>
    /// Gets or sets the argument type accepted by the triggered job.
    /// </summary>
    public Type? JobArgsType { get; set; }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Job"
        };
    }

    internal static ProjectUnit Create(
        BusinessTypeShape shape,
        ProjectUnitCatalog catalog,
        Type jobArgsType)
    {
        var unit = new UnitTriggeredJob(shape, catalog);
        unit.CheckNameConventionMode();
        unit.JobArgsType = jobArgsType;
        return unit;
    }
}

using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.HostedService;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.ProjectUnits.Services.Support;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes a host-managed service and its constructor dependencies.
/// </summary>
public sealed class UnitHostedService : ProjectUnit
{
    private UnitHostedService(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.HostedService, catalog, GetExecutionPoints(shape))
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    internal static ProjectUnit Create(BusinessTypeShape shape, ProjectUnitCatalog catalog)
    {
        var unit = new UnitHostedService(shape, catalog);
        unit.CheckNameConventionMode();
        unit.InitializeMethods<IHostedService>();
        return unit;
    }

    private static ExecutionPoint[] GetExecutionPoints(BusinessTypeShape shape)
    {
        if (!shape.IsAssignableTo(typeof(MoHostedService))
            && !shape.IsAssignableTo(typeof(MoBackgroundService)))
        {
            return [];
        }

        return
        [
            HostedServiceExecutionPoints.Start,
            HostedServiceExecutionPoints.Stop,
            HostedServiceExecutionPoints.WorkItem
        ];
    }
}

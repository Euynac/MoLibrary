using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.HostedService;
using Monica.Core.HostedService.Abstractions;
using Monica.ProjectUnits.Services.Support;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes a host-managed service and its constructor dependencies.
/// </summary>
public sealed class UnitHostedService : ProjectUnit
{
    private UnitHostedService(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.HostedService, catalog, GetExecutionPoints(type))
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    protected override bool VerifyTypeConstrain()
    {
        return typeof(IHostedService).IsAssignableFrom(Type);
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitHostedService(type, catalog);
        if (!unit.VerifyType())
        {
            return null;
        }

        unit.InitializeMethods<IHostedService>();
        return unit;
    }

    private static ExecutionPoint[] GetExecutionPoints(Type type)
    {
        if (!typeof(MoHostedService).IsAssignableFrom(type)
            && !typeof(MoBackgroundService).IsAssignableFrom(type))
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

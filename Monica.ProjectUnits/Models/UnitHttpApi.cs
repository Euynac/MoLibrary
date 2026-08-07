using Microsoft.AspNetCore.Mvc;
using Monica.Core.Execution;
using Monica.Core.Execution.Mvc;
using Monica.Core.TypeDiscovery.Models;
using Monica.ProjectUnits.Services.Support;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes an ASP.NET Core MVC controller. Generated mediated controllers remain discoverable but do not advertise
/// the direct MVC execution point.
/// </summary>
public sealed class UnitHttpApi : ProjectUnit
{
    private UnitHttpApi(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.HttpApi, catalog, GetExecutionPoints(shape))
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    internal static ProjectUnit Create(BusinessTypeShape shape, ProjectUnitCatalog catalog)
    {
        var unit = new UnitHttpApi(shape, catalog);
        unit.CheckNameConventionMode();
        unit.InitializeMethods<ControllerBase>();
        return unit;
    }

    private static ExecutionPoint[] GetExecutionPoints(BusinessTypeShape shape)
    {
        return shape.HasAttribute(typeof(MediatedControllerAttribute), inherit: false)
            ? []
            : [MvcExecutionPoints.Action];
    }
}

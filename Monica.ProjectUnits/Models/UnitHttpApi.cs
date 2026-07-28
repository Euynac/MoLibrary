using Microsoft.AspNetCore.Mvc;
using Monica.Core.Execution;
using Monica.ProjectUnits.Services.Support;
using Monica.WebApi.AutoControllers;
using Monica.WebApi.AutoControllers.Abstractions;
using Monica.WebApi.AutoControllers.Annotations;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes an ASP.NET Core MVC controller. Generated mediated controllers remain discoverable but do not advertise
/// the direct MVC execution point.
/// </summary>
public sealed class UnitHttpApi : ProjectUnit
{
    private UnitHttpApi(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.HttpApi, catalog, GetExecutionPoints(type))
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    protected override bool VerifyTypeConstrain()
    {
        return typeof(ControllerBase).IsAssignableFrom(Type)
               && !typeof(ICrudApplicationService).IsAssignableFrom(Type);
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitHttpApi(type, catalog);
        if (!unit.VerifyType())
        {
            return null;
        }

        unit.InitializeMethods<ControllerBase>();
        return unit;
    }

    private static ExecutionPoint[] GetExecutionPoints(Type type)
    {
        return type.IsDefined(typeof(MediatedControllerAttribute), inherit: false)
            ? []
            : [MvcExecutionPoints.Action];
    }
}

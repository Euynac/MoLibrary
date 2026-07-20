using Monica.ProjectUnits.Services.Support;
using Monica.Tool.Extensions;
using Monica.WebApi.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Request class
/// </summary>
public class UnitRequestDto : ProjectUnit
{
    internal UnitRequestDto(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.RequestDto, catalog)
    {
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitRequestDto(type, catalog);
        if (!type.IsImplementInterface(typeof(IResultRequestBase))) return null;
        unit.CheckNameConventionMode();
        return unit;
    }
}

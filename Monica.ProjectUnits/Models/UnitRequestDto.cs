using Monica.Core.TypeDiscovery.Models;
using Monica.ProjectUnits.Services.Support;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Request class
/// </summary>
public class UnitRequestDto : ProjectUnit
{
    internal UnitRequestDto(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.RequestDto, catalog)
    {
    }

    internal static ProjectUnit Create(BusinessTypeShape shape, ProjectUnitCatalog catalog)
    {
        var unit = new UnitRequestDto(shape, catalog);
        unit.CheckNameConventionMode();
        return unit;
    }
}

using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder;
using Monica.Core.TypeDiscovery.Models;
using Monica.ProjectUnits.Services.Support;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes a startup seeder and its constructor dependencies.
/// </summary>
public sealed class UnitSeeder : ProjectUnit
{
    private UnitSeeder(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.Seeder, catalog, SeederExecutionPoints.Run)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    internal static ProjectUnit Create(BusinessTypeShape shape, ProjectUnitCatalog catalog)
    {
        var unit = new UnitSeeder(shape, catalog);
        unit.CheckNameConventionMode();
        unit.InitializeMethods<ISeeder>();
        return unit;
    }
}

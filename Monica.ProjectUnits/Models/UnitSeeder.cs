using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder;
using Monica.ProjectUnits.Services.Support;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes a startup seeder and its constructor dependencies.
/// </summary>
public sealed class UnitSeeder : ProjectUnit
{
    private UnitSeeder(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.Seeder, catalog, SeederExecutionPoints.Run)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    protected override bool VerifyTypeConstrain()
    {
        return typeof(ISeeder).IsAssignableFrom(Type);
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitSeeder(type, catalog);
        if (!unit.VerifyType())
        {
            return null;
        }

        unit.InitializeMethods<ISeeder>();
        return unit;
    }
}

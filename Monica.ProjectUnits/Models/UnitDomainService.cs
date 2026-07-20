using Monica.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.WebApi.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Domain services
/// </summary>
public class UnitDomainService : ProjectUnit
{
    internal UnitDomainService(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.DomainService, catalog)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(DomainService));
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Domain"
        };
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitDomainService(type, catalog);
        if (!unit.VerifyType()) return null;
        
        // Initialization method metadata
        unit.InitializeMethods<DomainService>();
        return unit;
    }
}

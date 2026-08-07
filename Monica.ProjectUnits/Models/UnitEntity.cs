using Monica.Core.TypeDiscovery.Models;
using Monica.Modules;
using Monica.ProjectUnits.Services.Support;
using Monica.Repository.Entity.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// entities and aggregates
/// </summary>
public class UnitEntity : ProjectUnit
{
    internal UnitEntity(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.Entity, catalog)
    {
    }

    /// <summary>
    /// Gets or sets whether the represented entity is an aggregate root.
    /// </summary>
    public bool IsAggregate { get; set; }

    /// <summary>
    ///  Need to add config.AddDbContext in Program.cs
    /// </summary>
    public UnitRepository? RepoUnit { get; set; }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            NamespaceContains = "Entities"
        };
    }

    internal static ProjectUnit Create(BusinessTypeShape shape, ProjectUnitCatalog catalog)
    {
        var unit = new UnitEntity(shape, catalog);
        unit.CheckNameConventionMode();
        return unit;
    }
    public override void DeclareRelevance(ProjectUnit unit, bool isDependent = false)
    {
        if (unit is UnitRepository {IsHistoryRepo: false} repo)
        {
            RepoUnit = repo;
        }

        base.DeclareRelevance(unit, isDependent);
    }
}

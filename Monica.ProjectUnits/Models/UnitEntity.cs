using Monica.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// entities and aggregates
/// </summary>
public class UnitEntity : ProjectUnit
{
    internal UnitEntity(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.Entity, catalog)
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

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsImplementInterface<IEntity>();
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            NamespaceContains = "Entities"
        };
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitEntity(type, catalog);
        return unit.VerifyType() ? unit : null;
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

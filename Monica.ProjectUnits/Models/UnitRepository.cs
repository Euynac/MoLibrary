using Microsoft.Extensions.Logging;
using Monica.Core.TypeDiscovery.Models;
using Monica.Modules;
using Monica.ProjectUnits.Services.Support;
using Monica.Repository.Entity.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Warehousing layer
/// </summary>
public class UnitRepository : ProjectUnit
{
    internal UnitRepository(BusinessTypeShape shape, ProjectUnitCatalog catalog)
        : base(shape, EProjectUnitType.Repository, catalog)
    {
    }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    /// <summary>
    /// Gets or sets whether the repository represents historical entity data.
    /// </summary>
    public bool IsHistoryRepo { get; set; }

    /// <summary>
    /// Gets or sets the entity type managed by the repository.
    /// </summary>
    public Type EntityType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the conventionally named repository interface implemented by the repository.
    /// </summary>
    public Type RepoInterface { get; set; } = null!;
    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Repository"
        };
    }

    internal static ProjectUnit Create(
        BusinessTypeShape shape,
        ProjectUnitCatalog catalog,
        Type entityType,
        Type repositoryInterface)
    {
        var unit = new UnitRepository(shape, catalog);
        unit.CheckNameConventionMode();
        unit.EntityType = entityType;
        unit.RepoInterface = repositoryInterface;
        unit.IsHistoryRepo = shape.Type.Name.EndsWith("History", StringComparison.Ordinal);

        return unit;
    }

    public override void DoingConnect()
    {
        if (Catalog.FindByFullName(EntityType.FullName) is not { } entityUnit)
        {
            var alertMessage =
                $"Repository project unit {this} could not associate entity {EntityType.GetCleanFullName()}; " +
                $"the entity may not derive from a supported {nameof(Entity)} base type.";
            Alerts.Add(new ProjectUnitAlert
            {
                Level = EAlertLevel.Warning,
                Message = alertMessage,
                Source = "EntityTypeAssociation"
            });
            Logger.LogWarning(alertMessage);
            base.DoingConnect();
            return;
        }

        DeclareRelevance(entityUnit, true);
        entityUnit.DeclareRelevance(this);
        base.DoingConnect();
    }
}

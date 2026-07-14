using Microsoft.Extensions.Logging;
using Monica.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Warehousing layer
/// </summary>
public class UnitRepository : ProjectUnit
{
    internal UnitRepository(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.Repository, catalog)
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

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitRepository(type, catalog);
        if (!type.IsImplementInterfaceGeneric(typeof(IRepository<>), out var exactGenericType)) return null;
        unit.CheckNameConventionMode();
        var repoInterface = type.GetInterface($"I{type.Name}");
        if (repoInterface == null)
        {
            unit.Logger.LogError(
                "Repository {RepositoryType} was discovered, but its expected interface I{RepositoryType} was not found.",
                type.Name,
                type.Name);
            return null;
        }

     

        unit.EntityType = exactGenericType.GetGenericArguments().First();
        unit.RepoInterface = repoInterface;
        unit.IsHistoryRepo = type.Name.EndsWith("History");

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

using Monica.Framework.Core.Interfaces;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core.Model;

/// <summary>
/// entities and aggregates
/// </summary>
/// <param name="type"></param>
public class UnitEntity(Type type) : ProjectUnit(type, EProjectUnitType.Entity), IHasProjectUnitFactory
{
    static UnitEntity()
    {
        AddUnitRegisterFactory(Factory);
    }

    public bool IsAggregate { get; set; }

    /// <summary>
    ///  Need to add config.AddDbContext in Program.cs
    /// </summary>
    public UnitRepository? RepoUnit { get; set; }

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsImplementInterface<IEntity>();
    }

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            NamespaceContains = "Entities"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var unit = new UnitEntity(context.Type);
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
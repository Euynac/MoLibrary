using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 实体及聚合
/// </summary>
/// <param name="type"></param>
public class UnitEntity(Type type) : ProjectUnit(type, EProjectUnitType.Entity), IHasProjectUnitFactory
{
    static UnitEntity()
    {
        AddFactory(Factory);
    }

    public bool IsAggregate { get; set; }

    /// <summary>
    ///  需要在Program.cs 添加 config.AddDbContext
    /// </summary>
    public UnitRepository? RepoUnit { get; set; }

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsImplementInterface<IMoEntity>();
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
    public override void AddDependency(ProjectUnit unit)
    {
        if (unit is UnitRepository {IsHistoryRepo: false} repo)
        {
            RepoUnit = repo;
        }
        base.AddDependency(unit);
    }
}
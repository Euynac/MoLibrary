using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Interfaces;

namespace BuildingBlocksPlatform.Core.Model;

/// <summary>
/// 仓储层
/// </summary>
/// <param name="type"></param>
public class UnitRepository(Type type) : ProjectUnit(type, EProjectUnitType.Repository), IHasProjectUnitFactory
{
    static UnitRepository()
    {
        AddFactory(Factory);
    }

    public bool IsHistoryRepo { get; set; }
    public Type EntityType { get; set; } = null!;
    public Type RepoInterface { get; set; } = null!;
    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            Prefix = "Repository"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var type = context.Type;
        var unit = new UnitRepository(type);
        if (Option.ConventionOptions.EnablePerformanceMode && !unit.VerifyNameConvention()) return null;
        if (!type.IsImplementInterfaceGeneric(typeof(IMoRepository<,>), out var exactGenericType)) return null;
        unit.CheckNameConventionMode();
        var repoInterface = type.GetInterface($"I{type.Name}");
        if (repoInterface == null)
        {
            Logger.LogError($"仓储层{type.Name}解析成功，但其接口 I{type.Name} 获取失败，可能接口未按照规范命名。");
            return null;
        }

        unit.EntityType = exactGenericType.GetGenericArguments().First();
        unit.RepoInterface = repoInterface;
        unit.IsHistoryRepo = type.Name.EndsWith("History");

        return unit;
    }

    public override void DoingConnect()
    {
        if(!ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(EntityType.FullName!, out var unit))
        {
            Logger.LogWarning($"{this}无法关联其实体{EntityType.FullName},可能未继承{nameof(MoEntity)}相关基类");
            return;
        }

        AddDependency(unit);
        unit.AddDependency(this);
    }
}
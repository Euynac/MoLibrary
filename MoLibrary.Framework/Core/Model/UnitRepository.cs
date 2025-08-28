using Microsoft.Extensions.Logging;
using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Framework.Modules;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 仓储层
/// </summary>
/// <param name="type"></param>
public class UnitRepository(Type type) : ProjectUnit(type, EProjectUnitType.Repository), IHasProjectUnitFactory
{
    static UnitRepository()
    {
        AddUnitRegisterFactory(Factory);
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
        if (!type.IsImplementInterfaceGeneric(typeof(IMoRepository<>), out var exactGenericType)) return null;
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
        if(!ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(EntityType.FullName!, out var entityUnit))
        {
            var alertMessage = $"{this}无法关联其实体{EntityType.GetCleanFullName()},可能未继承{nameof(MoEntity)}相关基类";
            // 添加警告级别告警
            Alerts.Add(new ProjectUnitAlert
            {
                Level = EAlertLevel.Warning,
                Message = alertMessage,
                Source = "EntityTypeAssociation"
            });
            Logger.LogWarning(alertMessage);
            return;
        }

        DeclareRelevance(entityUnit, true);
        entityUnit.DeclareRelevance(this);
    }
}
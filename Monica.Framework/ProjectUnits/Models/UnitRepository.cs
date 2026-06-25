using Microsoft.Extensions.Logging;
using Monica.Framework.ProjectUnits.Abstractions;
using Monica.Framework.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.Framework.ProjectUnits.Models;

/// <summary>
/// Warehousing layer
/// </summary>
/// <param name="type"></param>
public class UnitRepository(Type type) : ProjectUnit(type, EProjectUnitType.Repository), IHasProjectUnitFactory
{
    protected override bool ShouldAnalyzeConstructorDependencies => true;

    static UnitRepository()
    {
        AddUnitRegisterFactory(Factory);
    }

    public bool IsHistoryRepo { get; set; }
    public Type EntityType { get; set; } = null!;
    public Type RepoInterface { get; set; } = null!;
    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Repository"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var type = context.Type;
        var unit = new UnitRepository(type);
        if (!type.IsImplementInterfaceGeneric(typeof(IRepository<>), out var exactGenericType)) return null;
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
        if(!ProjectUnitRegistry.ProjectUnitsByFullName.TryGetValue(EntityType.FullName!, out var entityUnit))
        {
            var alertMessage = $"{this}无法关联其实体{EntityType.GetCleanFullName()},可能未继承{nameof(Entity)}相关基类";
            // Add warning level alert
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

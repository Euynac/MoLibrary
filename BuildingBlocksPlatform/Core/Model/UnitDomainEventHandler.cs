using BuildingBlocksPlatform.EventBus.Abstractions;
using BuildingBlocksPlatform.EventBus.Events;
using BuildingBlocksPlatform.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Core.Model;

/// <summary>
/// 领域事件处理
/// </summary>
/// <param name="type"></param>
public class UnitDomainEventHandler(Type type) : ProjectUnit(type, EProjectUnitType.DomainEventHandler), IHasProjectUnitFactory
{
    static UnitDomainEventHandler()
    {
        AddFactory(Factory);
    }

    /// <summary>
    /// 领域事件类型
    /// </summary>
    public Type EventType { get; set; } = null!;

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            Prefix = "DomainEventHandler"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var type = context.Type;
        var unit = new UnitDomainEventHandler(type);
        if (Option.ConventionOptions.EnablePerformanceMode && !unit.VerifyNameConvention()) return null;
        if (!type.IsClass ||
            !type.IsImplementInterfaceGeneric(typeof(IDistributedEventHandler<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        context.ServiceCollection.Configure<DistributedEventBusOptions>(options =>
        {
            options.Handlers.Add(type);
        });
        unit.EventType = genericType.GetGenericArguments().First();
        return unit;
    }

    public override void DoingConnect()
    {
        if (!ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(EventType.FullName!, out var unit))
        {
            Logger.LogWarning($"{this}无法关联其领域事件基类{EventType.FullName}，可能未继承{nameof(OurDomainEvent)}");
            return;
        }

        AddDependency(unit);
        unit.AddDependency(this);
    }
}
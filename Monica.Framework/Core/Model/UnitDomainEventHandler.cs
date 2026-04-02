using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Events;
using Monica.Framework.Core.Interfaces;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core.Model;

/// <summary>
/// Domain event handling
/// </summary>
/// <param name="type"></param>
public class UnitDomainEventHandler(Type type) : ProjectUnit(type, EProjectUnitType.DomainEventHandler), IHasProjectUnitFactory
{
    static UnitDomainEventHandler()
    {
        AddUnitRegisterFactory(Factory);
    }

    /// <summary>
    /// Domain event type
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
        if (!type.IsClass ||
            !type.IsImplementInterfaceGeneric(typeof(IDistributedEventHandler<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        unit.EventType = genericType.GetGenericArguments().First();
        return unit;
    }

    public override void DoingConnect()
    {
        if (!ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(EventType.FullName!, out var eventUnit))
        {
            var alertMessage = $"{this}无法关联其领域事件基类{EventType.GetCleanFullName()}，可能未继承{nameof(DomainEvent)}";
            // Add warning level alert
            Alerts.Add(new ProjectUnitAlert
            {
                Level = EAlertLevel.Warning,
                Message = alertMessage,
                Source = "EventTypeAssociation"
            });
            Logger.LogWarning(alertMessage);
            return;
        }

        DeclareRelevance(eventUnit, true);
        eventUnit.DeclareRelevance(this);
    }
}
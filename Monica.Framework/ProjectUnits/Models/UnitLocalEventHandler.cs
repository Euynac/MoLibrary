using Monica.EventBus.Abstractions.Handlers;
using Monica.Framework.ProjectUnits.Abstractions;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.ProjectUnits.Models;

/// <summary>
/// local event handling
/// </summary>
/// <param name="type"></param>
public class UnitLocalEventHandler(Type type) : ProjectUnit(type, EProjectUnitType.LocalEventHandler), IHasProjectUnitFactory
{
    static UnitLocalEventHandler()
    {
        AddUnitRegisterFactory(Factory);
    }

    /// <summary>
    /// local event type
    /// </summary>
    public Type EventType { get; set; } = null!;

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "LocalEventHandler"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var type = context.Type;
        var unit = new UnitLocalEventHandler(type);
        if (!type.IsClass ||
            !type.IsImplementInterfaceGeneric(typeof(ILocalEventHandler<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        unit.EventType = genericType.GetGenericArguments().First();
        return unit;
    }
}
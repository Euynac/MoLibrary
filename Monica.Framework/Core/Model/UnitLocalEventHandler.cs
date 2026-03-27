using Monica.EventBus.Abstractions.Handlers;
using Monica.Framework.Core.Interfaces;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core.Model;

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

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            Prefix = "LocalEventHandler"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var type = context.Type;
        var unit = new UnitLocalEventHandler(type);
        if (!type.IsClass ||
            !type.IsImplementInterfaceGeneric(typeof(IMoLocalEventHandler<>), out var genericType) || genericType?.FullName is null) return null;
        unit.CheckNameConventionMode();
        unit.EventType = genericType.GetGenericArguments().First();
        return unit;
    }
}
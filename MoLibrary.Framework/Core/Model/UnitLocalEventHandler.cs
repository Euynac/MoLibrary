using Microsoft.Extensions.DependencyInjection;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Framework.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 本地事件处理
/// </summary>
/// <param name="type"></param>
public class UnitLocalEventHandler(Type type) : ProjectUnit(type, EProjectUnitType.LocalEventHandler), IHasProjectUnitFactory
{
    static UnitLocalEventHandler()
    {
        AddFactory(Factory);
    }

    /// <summary>
    /// 本地事件类型
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
        context.ServiceCollection.Configure<LocalEventBusOptions>(options =>
        {
            options.Handlers.Add(type);
        });
        unit.EventType = genericType.GetGenericArguments().First();
        return unit;
    }
}
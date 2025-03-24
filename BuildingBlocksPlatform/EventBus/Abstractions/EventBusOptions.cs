using MoLibrary.Tool.Utils;

namespace BuildingBlocksPlatform.EventBus.Abstractions;

public class DistributedEventBusOptions
{
    public ITypeList<IEventHandler> Handlers { get; } = new TypeList<IEventHandler>();
}

public class LocalEventBusOptions
{
    public ITypeList<IEventHandler> Handlers { get; } = new TypeList<IEventHandler>();
}

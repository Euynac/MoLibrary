using MoLibrary.Tool.Utils;

namespace MoLibrary.EventBus.Abstractions;

public class DistributedEventBusOptions
{
    public ITypeList<IMoEventHandler> Handlers { get; } = new TypeList<IMoEventHandler>();
}

public class LocalEventBusOptions
{
    public ITypeList<IMoEventHandler> Handlers { get; } = new TypeList<IMoEventHandler>();
}

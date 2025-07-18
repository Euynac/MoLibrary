using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Dapr.Modules;

public class ModuleDaprEventBusOption : MoModuleControllerOption<ModuleDaprEventBus>
{
    public string PubSubName { get; set; } = "pubsub";
    public string DaprEventBusCallback { get; set; } = "api/event-bus/dapr/event";

    /// <summary>
    /// 默认大批量事件批处理数量，为null则不进行分批推送。
    /// </summary>
    public int? BulkChunkSize { get; set; } = 1000;
}
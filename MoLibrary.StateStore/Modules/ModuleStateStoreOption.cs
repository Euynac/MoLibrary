using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.StateStore.Modules;

public class ModuleStateStoreOption : MoModuleOption<ModuleStateStore>
{
    /// <summary>
    /// 使用分布式状态存储作为默认的（非Keyed服务） <see cref="IMoStateStore"/> 实现
    /// </summary>
    public bool UseDistributedProviderAsDefault { get; set; }
}
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Locker.Modules;

public class ModuleLockerOption : MoModuleOption<ModuleLocker>
{
    /// <summary>
    /// DistributedLock key prefix.
    /// </summary>
    public string KeyPrefix { get; set; } = "";
}
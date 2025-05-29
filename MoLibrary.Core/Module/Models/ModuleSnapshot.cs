using MoLibrary.Core.Module.Features;

namespace MoLibrary.Core.Module.Models;

public class ModuleSnapshot(MoModule moduleInstance, ModuleRequestInfo requestInfo)
{
    public MoModule ModuleInstance { get; set; } = moduleInstance;
    public ModuleRequestInfo RequestInfo { get; set; } = requestInfo;
    public Type ModuleType { get; set; } = moduleInstance.GetType();
    public bool IsDisabled => ModuleManager.IsModuleDisabled(ModuleType);
}
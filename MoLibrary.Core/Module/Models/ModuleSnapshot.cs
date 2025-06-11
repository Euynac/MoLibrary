using MoLibrary.Core.Module.Features;

namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 表示模块的快照信息，包含模块实例、请求信息和状态。
/// </summary>
/// <param name="moduleInstance">模块实例</param>
/// <param name="registerInfo">模块请求信息</param>
public class ModuleSnapshot(MoModule moduleInstance, ModuleRegisterInfo registerInfo)
{
    /// <summary>
    /// 模块实例
    /// </summary>
    public MoModule ModuleInstance { get; set; } = moduleInstance;
    
    /// <summary>
    /// 模块请求信息
    /// </summary>
    public ModuleRegisterInfo RegisterInfo { get; set; } = registerInfo;
    
    /// <summary>
    /// 模块类型
    /// </summary>
    public Type ModuleType { get; set; } = moduleInstance.GetType();
    
    /// <summary>
    /// 获取模块对应的枚举值
    /// </summary>
    public EMoModules ModuleEnum =>
        ModuleAnalyser.ModuleTypeToEnumMap.TryGetValue(ModuleType, out var moduleEnum) ? moduleEnum :
            EMoModules.Developer;

    /// <summary>
    /// 获取模块的总初始化耗时（毫秒）
    /// </summary>
    public long TotalInitializationDurationMs =>
        ModuleProfiler.GetModuleTotalDuration(ModuleType);


    public override string ToString()
    {
        return $"[{ModuleEnum}] {RegisterInfo}";
    }
}
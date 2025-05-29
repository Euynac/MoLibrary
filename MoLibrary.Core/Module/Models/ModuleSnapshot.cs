using MoLibrary.Core.Module.Features;

namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 表示模块的快照信息，包含模块实例、请求信息和状态。
/// </summary>
/// <param name="moduleInstance">模块实例</param>
/// <param name="requestInfo">模块请求信息</param>
public class ModuleSnapshot(MoModule moduleInstance, ModuleRequestInfo requestInfo)
{
    /// <summary>
    /// 模块实例
    /// </summary>
    public MoModule ModuleInstance { get; set; } = moduleInstance;
    
    /// <summary>
    /// 模块请求信息
    /// </summary>
    public ModuleRequestInfo RequestInfo { get; set; } = requestInfo;
    
    /// <summary>
    /// 模块类型
    /// </summary>
    public Type ModuleType { get; set; } = moduleInstance.GetType();
    
    /// <summary>
    /// 模块是否被禁用
    /// </summary>
    public bool IsDisabled => ModuleManager.IsModuleDisabled(ModuleType);
    
    /// <summary>
    /// 获取模块对应的枚举值
    /// </summary>
    public EMoModules ModuleEnum
    {
        get
        {
            if (ModuleAnalyser.ModuleTypeToEnumMap.TryGetValue(ModuleType, out var moduleEnum))
            {
                return moduleEnum;
            }
            // 如果没有找到对应的枚举，返回Developer作为默认值
            return EMoModules.Developer;
        }
    }
}
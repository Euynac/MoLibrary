using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// 异常池模块配置引导类
/// 提供流畅的API用于配置异常池模块
/// </summary>
public class ModuleExceptionPoolGuide
    : MoModuleGuide<ModuleExceptionPool, ModuleExceptionPoolOption, ModuleExceptionPoolGuide>
{
    public override EMoModules GetTargetModuleEnum()
    {
        return EMoModules.ExceptionPool;
    }

    /// <summary>
    /// 设置全局默认异常池最大容量
    /// </summary>
    /// <param name="size">最大容量</param>
    /// <returns>当前引导实例，支持链式调用</returns>
    public ModuleExceptionPoolGuide SetGlobalDefaultMaxSize(int size)
    {
        ConfigureModuleOption(option =>
        {
            option.GlobalOption.DefaultMaxSize = size;
        });
        return this;
    }

    /// <summary>
    /// 设置全局是否启用事件触发
    /// </summary>
    /// <param name="enable">是否启用</param>
    /// <returns>当前引导实例，支持链式调用</returns>
    public ModuleExceptionPoolGuide SetGlobalEnableEventTrigger(bool enable)
    {
        ConfigureModuleOption(option =>
        {
            option.GlobalOption.EnableEventTrigger = enable;
        });
        return this;
    }
}

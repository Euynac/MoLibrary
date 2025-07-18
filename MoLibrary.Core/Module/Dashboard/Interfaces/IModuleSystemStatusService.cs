using MoLibrary.Core.Module.Dashboard.Models;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard.Interfaces;

/// <summary>
/// 提供模块系统状态、性能、依赖关系等信息的服务接口。
/// 用于支持界面展示和系统监控。
/// </summary>
public interface IModuleSystemStatusService
{
    /// <summary>
    /// 获取模块系统的整体状态信息。
    /// </summary>
    /// <returns>模块系统状态信息</returns>
    ModuleSystemStatus GetSystemStatus();

    /// <summary>
    /// 获取模块系统的性能信息。
    /// </summary>
    /// <returns>模块系统性能信息</returns>
    ModuleSystemPerformance GetSystemPerformance();

    /// <summary>
    /// 获取所有模块的注册和依赖关系信息。
    /// </summary>
    /// <returns>模块注册信息列表</returns>
    ModuleRegistrationInfo GetRegistrationInfo();

    /// <summary>
    /// 获取指定模块的详细信息。
    /// </summary>
    /// <param name="moduleType">模块类型</param>
    /// <returns>模块详细信息，如果模块不存在则返回null</returns>
    ModuleDetailInfo? GetModuleDetail(Type moduleType);

    /// <summary>
    /// 获取指定模块的详细信息。
    /// </summary>
    /// <param name="moduleEnum">模块枚举</param>
    /// <returns>模块详细信息，如果模块不存在则返回null</returns>
    ModuleDetailInfo? GetModuleDetail(EMoModules moduleEnum);

    /// <summary>
    /// 获取模块依赖关系图信息。
    /// </summary>
    /// <returns>模块依赖关系图</returns>
    ModuleDependencyGraph GetDependencyGraph();

    /// <summary>
    /// 获取模块系统的健康状态检查结果。
    /// </summary>
    /// <returns>健康状态检查结果</returns>
    ModuleSystemHealthCheck GetHealthCheck();
} 
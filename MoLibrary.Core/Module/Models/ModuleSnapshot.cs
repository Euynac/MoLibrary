using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module.Features;
using MoLibrary.Core.Module.Interfaces;

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
    public IMoModule ModuleInstance { get; set; } = moduleInstance;

    /// <summary>
    /// 模块请求信息
    /// </summary>
    public ModuleRegisterInfo RegisterInfo { get; set; } = registerInfo;

    /// <summary>
    /// 模块类型
    /// </summary>
    public Type ModuleType { get; set; } = moduleInstance.GetType();

    /// <summary>
    /// 获取模块对应的ModuleKey。对于未注册模块返回 null。
    /// </summary>
    public ModuleKey? ModuleKey => ModuleAnalyser.ModuleTypeToKeyMap.GetValueOrDefault(ModuleType);

    /// <summary>
    /// 获取模块的总初始化耗时（毫秒）
    /// </summary>
    public long TotalInitializationDurationMs =>
        ModuleProfiler.GetModuleTotalDuration(ModuleType);


    public override string ToString()
    {
        var moduleKeyDisplay = ModuleKey?.ToString() ?? "Unknown";
        return $"[{moduleKeyDisplay}] {RegisterInfo}";
    }

    /// <summary>
    /// Gets the keyed option for this module from DI container.
    /// Uses ModuleOptionType from RegisterInfo and IOptionsSnapshot for retrieval.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="serviceKey">The keyed service key, or null for default option</param>
    /// <returns>Tuple of option type and option instance</returns>
    public (Type OptionType, object? OptionInstance) GetKeyedOption(
        IServiceProvider serviceProvider,
        string? serviceKey)
    {
        var optionType = RegisterInfo.ModuleOptionType;

        if (serviceKey == null)
        {
            // For non-keyed (default) option, use IOptions<T>
            var optionsType = typeof(IOptions<>).MakeGenericType(optionType);
            var options = serviceProvider.GetService(optionsType);
            var value = options?.GetType().GetProperty("Value")?.GetValue(options);
            return (optionType, value);
        }

        // For keyed option, use IOptionsSnapshot<T>.Get(key)
        var snapshotType = typeof(IOptionsSnapshot<>).MakeGenericType(optionType);
        var snapshot = serviceProvider.GetService(snapshotType);
        if (snapshot == null) return (optionType, null);

        var getMethod = snapshotType.GetMethod("Get");
        var instance = getMethod?.Invoke(snapshot, [serviceKey]);
        return (optionType, instance);
    }
}
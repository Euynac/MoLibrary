using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Features.MoScopedData;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleScopedDataBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ScopedData 模块
        /// </summary>
        public static ModuleScopedDataGuide AddScopedData(Action<ModuleScopedDataOption>? action = null)
        {
            return new ModuleScopedDataGuide().Register(action);
        }
    }
}

/// <summary>
/// ScopedData模块，用于在Scoped生命周期内管理环境数据
/// </summary>
public class ModuleScopedData(ModuleScopedDataOption option)
    : MoModule<ModuleScopedData, ModuleScopedDataOption, ModuleScopedDataGuide>(option)
{
    /// <summary>
    /// 配置服务注册
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册ScopedData服务为Scoped生命周期
        services.AddScoped<IMoScopedData, MoScopedDataDefaultScopedProvider>();
        
        base.ConfigureServices(services);
    }

    /// <summary>
    /// 获取当前模块枚举
    /// </summary>
    /// <returns>模块枚举值</returns>
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ScopedData;
    }
}

/// <summary>
/// ScopedData模块指导器
/// </summary>
public class ModuleScopedDataGuide : MoModuleGuide<ModuleScopedData, ModuleScopedDataOption, ModuleScopedDataGuide>
{
    /// <summary>
    /// 注册指定键的环境数据服务
    /// </summary>
    /// <typeparam name="T">环境数据实现类型，必须实现IMoScopedData接口</typeparam>
    /// <param name="key">服务键</param>
    /// <returns>返回当前模块指南实例以支持链式调用</returns>
    public ModuleScopedDataGuide AddKeyedScopedData<T>(string key) where T : class, IMoScopedData
    {
        ConfigureServices(context =>
            {
                context.Services.AddKeyedScoped<IMoScopedData, T>(key);
            }, secondKey: key);

        RecordKeyedServiceKey(key);
        return this;
    }

}

/// <summary>
/// ScopedData模块配置选项
/// </summary>
public class ModuleScopedDataOption : MoModuleOption<ModuleScopedData>
{
    // 可以在这里添加特定的配置选项
}

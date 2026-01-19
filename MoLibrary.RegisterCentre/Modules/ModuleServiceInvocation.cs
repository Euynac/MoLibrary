using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.RegisterCentre.ServiceInvocation.Implements;
using MoLibrary.RegisterCentre.ServiceInvocation.Interfaces;

namespace MoLibrary.RegisterCentre.Modules;

/// <summary>
/// 服务调用模块
/// </summary>
public class ModuleServiceInvocation(ModuleServiceInvocationOption option)
    : MoModuleWithDependencies<ModuleServiceInvocation, ModuleServiceInvocationOption, ModuleServiceInvocationGuide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.ServiceInvocation;

    public override void ClaimDependencies()
    {
        // 服务调用模块无依赖
    }
}

/// <summary>
/// 服务调用模块配置选项
/// </summary>
public class ModuleServiceInvocationOption : MoModuleOption<ModuleServiceInvocation>
{
    /// <summary>
    /// 是否使用分布式调用提供者
    /// </summary>
    public bool UseDistributedProvider { get; internal set; }
}

/// <summary>
/// 服务调用模块配置指南
/// </summary>
public class ModuleServiceInvocationGuide : MoModuleGuide<ModuleServiceInvocation, ModuleServiceInvocationOption, ModuleServiceInvocationGuide>
{
    private const string SET_PROVIDER = nameof(SET_PROVIDER);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [SET_PROVIDER];
    }

    /// <summary>
    /// 使用独立模式（不支持服务调用，调用时抛出异常）
    /// </summary>
    public ModuleServiceInvocationGuide UseStandaloneProvider()
    {
        ConfigureEmpty(SET_PROVIDER);
        ConfigureModuleOption(o => o.UseDistributedProvider = false);
        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IServiceInvocationConnector, StandaloneServiceInvocationProvider>();
        });
        return this;
    }

    /// <summary>
    /// 使用分布式调用提供者
    /// </summary>
    /// <typeparam name="TProvider">提供者类型</typeparam>
    public ModuleServiceInvocationGuide UseDistributedProvider<TProvider>()
        where TProvider : class, IServiceInvocationConnector
    {
        ConfigureEmpty(SET_PROVIDER);
        ConfigureModuleOption(o => o.UseDistributedProvider = true);
        ConfigureServices(context =>
        {
            context.Services.TryAddSingleton<IServiceInvocationConnector, TProvider>();
        });
        return this;
    }
}

/// <summary>
/// 服务调用模块构建器扩展
/// </summary>
public static class ModuleServiceInvocationBuilderExtensions
{
    /// <summary>
    /// 配置服务调用模块
    /// </summary>
    public static ModuleServiceInvocationGuide ConfigModuleServiceInvocation(
        this WebApplicationBuilder builder,
        Action<ModuleServiceInvocationOption>? action = null)
    {
        return new ModuleServiceInvocationGuide().Register(action);
    }
}

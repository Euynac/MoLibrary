using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Module;

public class ModuleRegisterRequest(string key)
{
    public Action<IServiceCollection>? ConfigureServices { get; set; }
    public string Key { get; set; } = key;

    /// <summary>
    /// 指示当前Key的请求只能配置一次
    /// </summary>
    public bool OnlyConfigOnce { get; set; }
    public EMoModules? RequestFrom { get; set; }
    public int Order { get; set; }
}

public class MoModuleRegisterCentre
{
    public static Dictionary<Type, List<ModuleRegisterRequest>> ModuleRegisterContextDict { get; set; } = new();
}


public abstract class MoModule : IMoModule
{
    /// <summary>
    /// 配置WebApplicationBuilder
    /// 默认实现为空，子类可根据需要重写
    /// </summary>
    /// <param name="builder">WebApplicationBuilder实例</param>
    public virtual void ConfigureBuilder(WebApplicationBuilder builder)
    {
    }

    /// <summary>
    /// 配置服务依赖注入
    /// 默认实现为空，子类可根据需要重写
    /// </summary>
    /// <param name="services">服务集合</param>
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// 使用中间件
    /// 默认实现为空，子类可根据需要重写
    /// </summary>
    /// <param name="application">应用程序构建器</param>
    public virtual void UseMiddlewares(IApplicationBuilder application)
    {
    }

    public abstract EMoModules GetMoModuleEnum();

    public static TModuleGuide Register<TModule, TModuleOption, TModuleGuide>() where TModule : MoModule
        where TModuleOption : IMoModuleOption<TModule>
        where TModuleGuide : MoModuleGuide<TModule>, new()
    {
        return new TModuleGuide();
    }
}

/// <summary>
/// MoLibrary模块抽象基类
/// 提供IMoLibraryModule接口的默认实现
/// </summary>
public abstract class MoModule<TModuleSelf, TModuleOption, TModuleGuide> : MoModule 
    where TModuleOption : IMoModuleOption<TModuleSelf> 
    where TModuleGuide : MoModuleGuide<TModuleSelf>, new()
    where TModuleSelf : MoModule<TModuleSelf, TModuleOption, TModuleGuide>
{

    public void DependsOnModule(params EMoModules[] modules)
    {

    }
    public TModuleGuide DependsOnModule<TOtherModuleOption>(Action<TOtherModuleOption>? preConfig = null,
        Action<TOtherModuleOption>? postConfig = null) 
        where TOtherModuleOption : IMoModuleOption
    {
        return new TModuleGuide()
        {
            GuideFrom = GetMoModuleEnum()
        };
    }
    
}
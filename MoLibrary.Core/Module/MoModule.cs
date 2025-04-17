using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Tool.MoResponse;

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
    public virtual Res ConfigureBuilder(WebApplicationBuilder builder)
    {
        return Res.Ok();
    }

    public virtual Res ConfigureServices(IServiceCollection services)
    {
        return Res.Ok();
    }
  
    public virtual Res PostConfigureServices(IServiceCollection services)
    {
        return Res.Ok();
    }
    public virtual Res UseMiddlewares(IApplicationBuilder app)
    {
        return Res.Ok();
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
public abstract class MoModule<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option) : MoModule 
    where TModuleOption : IMoModuleOption<TModuleSelf>, new() 
    where TModuleGuide : MoModuleGuide<TModuleSelf>, new()
    where TModuleSelf : MoModule<TModuleSelf, TModuleOption, TModuleGuide>
{
    public ILogger<TModuleSelf> Logger { get; set; } = NullLogger<TModuleSelf>.Instance;
    public TModuleOption Option { get; } = option;
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
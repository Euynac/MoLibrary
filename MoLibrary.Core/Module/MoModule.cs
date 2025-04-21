using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Module;
public class ModuleRegisterContext(IServiceCollection services, object option)
{
    public IServiceCollection Services { get; init; } = services;
    public object Option { get; init; } = option;

}
public class ModuleRegisterContext<TModuleOption>(IServiceCollection services, TModuleOption option) : ModuleRegisterContext(services, option)
{
    public TModuleOption OptionInstance => (TModuleOption)Option;
}

public class ModuleRegisterRequest(string key)
{
    public Action<ModuleRegisterContext>? ConfigureContext { get; set; }
    public string Key { get; set; } = key;

    /// <summary>
    /// 指示当前Key的请求只能配置一次
    /// </summary>
    public bool OnlyConfigOnce { get; set; }
    public EMoModules? RequestFrom { get; set; }
    public int Order { get; set; }

    public void SetConfigureContext<TModuleOption>(Action<ModuleRegisterContext<TModuleOption>> context)
    {
        ConfigureContext = registerContext =>
        {
            context.Invoke((ModuleRegisterContext<TModuleOption>) registerContext);
        };
    }
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

    public abstract EMoModules CurModuleEnum();
}

/// <summary>
/// MoLibrary模块抽象基类
/// 提供IMoLibraryModule接口的默认实现
/// </summary>
public abstract class MoModule<TModuleSelf, TModuleOption>(TModuleOption option) : MoModule 
    where TModuleOption : IMoModuleOption<TModuleSelf>, new() 
    where TModuleSelf : MoModule<TModuleSelf, TModuleOption>
{
    public ILogger<TModuleSelf> Logger { get; set; } = NullLogger<TModuleSelf>.Instance;
    public TModuleOption Option { get; } = option;
}

public abstract class MoModuleWithDependencies<TModuleSelf, TModuleOption>(TModuleOption option) : MoModule<TModuleSelf, TModuleOption>(option), IWantDependsOnOtherModules
    where TModuleOption : IMoModuleOption<TModuleSelf>, new()
    where TModuleSelf : MoModuleWithDependencies<TModuleSelf, TModuleOption>
{
    public List<EMoModules> DependedModules { get; set; } = [];
  
    /// <summary>
    /// 声明依赖的模块，并进行配置等
    /// </summary>
    public abstract void ClaimDependencies();
   
    protected TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()  
        where TOtherModuleGuide : MoModuleGuide, new()
    {
        return new TOtherModuleGuide()
        {
            GuideFrom = CurModuleEnum()
        };
    }
    
}



public interface IWantDependsOnOtherModules
{
    public List<EMoModules> DependedModules { get; set; } 
}

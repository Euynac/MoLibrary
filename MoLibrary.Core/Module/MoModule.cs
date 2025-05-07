using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Module;

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
    public virtual Res ConfigureApplicationBuilder(IApplicationBuilder app)
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
    public TModuleOption Option { get; } = option;
    public ILogger<TModuleSelf> Logger { get; set; } = NullLogger<TModuleSelf>.Instance;
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
    /// <summary>
    /// 声明依赖的模块，并进行配置等
    /// </summary>
    public void ClaimDependencies();
}

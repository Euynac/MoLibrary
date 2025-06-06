using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Features;
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

    public virtual Res ConfigureEndpoints(IApplicationBuilder app)
    {
        return Res.Ok();
    }

    public abstract EMoModules CurModuleEnum();
}


/// <summary>
/// MoLibrary模块抽象基类
/// 提供IMoLibraryModule接口的默认实现
/// </summary>
public abstract class MoModule<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option) : MoModule, IMoModuleStaticInfo, IMoModuleGuideBridge
    where TModuleOption : MoModuleOption<TModuleSelf>, new() 
    where TModuleSelf : MoModule<TModuleSelf, TModuleOption, TModuleGuide>
    where TModuleGuide : MoModuleGuide<TModuleSelf, TModuleOption, TModuleGuide>, new()
{
    public TModuleOption Option { get; } = option;
    public ILogger Logger { get;  } = option.Logger;
    
    /// <summary>
    /// Gets the enum value representing this module type.
    /// This static method creates a temporary instance to access the CurModuleEnum method.
    /// </summary>
    /// <returns>The EMoModules value representing this module.</returns>
    public static EMoModules GetModuleEnum()
    {
        // Create a temporary instance with default options to get the module enum
        var instance = Activator.CreateInstance(typeof(TModuleSelf), new TModuleOption()) as TModuleSelf;
        var moduleEnum = instance!.CurModuleEnum();
        
        // Register the mapping between module type and enum
        ModuleAnalyser.RegisterModuleMapping(typeof(TModuleSelf), moduleEnum);
        
        return moduleEnum;
    }

    public void CheckRequiredMethod(string methodName, string? errorDetail = null)
    {
        new TModuleGuide().CheckRequiredMethod(methodName, errorDetail);
    }
}

public abstract class MoModuleWithDependencies<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option) : MoModule<TModuleSelf, TModuleOption, TModuleGuide>(option), IWantDependsOnOtherModules
    where TModuleOption : MoModuleOption<TModuleSelf>, new()
    where TModuleSelf : MoModuleWithDependencies<TModuleSelf, TModuleOption, TModuleGuide>
    where TModuleGuide : MoModuleGuide<TModuleSelf, TModuleOption, TModuleGuide>, new()
{
    /// <summary>
    /// 声明依赖的模块，并进行配置等
    /// </summary>
    public abstract void ClaimDependencies();
   
   
    protected TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()  
        where TOtherModuleGuide : MoModuleGuide, new()
    {
        return MoModuleGuide.DeclareDependency<TOtherModuleGuide>(CurModuleEnum(), CurModuleEnum());
    }
}

public interface IWantDependsOnOtherModules
{
    /// <summary>
    /// 声明依赖的模块，并进行配置等
    /// </summary>
    public void ClaimDependencies();
}
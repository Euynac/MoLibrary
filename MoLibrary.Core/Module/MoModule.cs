using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.ModuleAnalyser;
using MoLibrary.Tool.MoResponse;
using System;
using System.Collections.Generic;
using MoLibrary.Core.Features.MoLogProvider;

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
public abstract class MoModule<TModuleSelf, TModuleOption>(TModuleOption option) : MoModule, IMoModuleStaticInfo
    where TModuleOption : IMoModuleOption<TModuleSelf>, new() 
    where TModuleSelf : MoModule<TModuleSelf, TModuleOption>
{
    public TModuleOption Option { get; } = option;
    public ILogger Logger { get; set; } = LogProvider.For<TModuleSelf>();
    
    /// <summary>
    /// Gets the enum value representing this module type.
    /// This static method creates a temporary instance to access the CurModuleEnum method.
    /// </summary>
    /// <returns>The EMoModules value representing this module.</returns>
    public static EMoModules GetModuleEnum()
    {
        // Create a temporary instance with default options to get the module enum
        var instance = Activator.CreateInstance(typeof(TModuleSelf), new TModuleOption()) as TModuleSelf;
        return instance!.CurModuleEnum();
    }
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
   
    /// <summary>
    /// Declares a dependency on another module and returns a guide for configuring that module.
    /// </summary>
    /// <typeparam name="TOtherModuleGuide">Type of the module guide for the dependent module.</typeparam>
    /// <returns>A module guide for configuring the dependent module.</returns>
    protected TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()  
        where TOtherModuleGuide : MoModuleGuide, new()
    {
        // Add dependency to the list if it's not already there
        var targetModule = new TOtherModuleGuide().GetTargetModuleEnum();
        if (!DependedModules.Contains(targetModule))
        {
            DependedModules.Add(targetModule);
            
            // Register this dependency relationship in the ModuleAnalyser
            MoModuleAnalyser.AddDependency(CurModuleEnum(), targetModule);
        }
        
        // Create and return the module guide
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

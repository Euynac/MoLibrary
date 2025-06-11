using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Features;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module;

public abstract class MoModule : IMoModule
{
    public virtual void ConfigureBuilder(WebApplicationBuilder builder)
    {
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
    }
  
    public virtual void PostConfigureServices(IServiceCollection services)
    {
    }
    public virtual void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
    }

    public virtual void ConfigureEndpoints(IApplicationBuilder app)
    {
    }

    public abstract EMoModules CurModuleEnum();
    internal abstract void ConvertToRegisterRequest();
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

    internal override void ConvertToRegisterRequest()
    {
        var guide = new TModuleGuide();

        guide.ConfigureBuilder(context =>
        {
            ConfigureBuilder(context.WebApplicationBuilder);
        }, -1);

        guide.ConfigureServices(context =>
        {
            ConfigureServices(context.Services);
        });

        guide.PostConfigureServices(context =>
        {
            PostConfigureServices(context.Services);
        });

        guide.ConfigureApplicationBuilder(context =>
        {
            ConfigureApplicationBuilder(context.ApplicationBuilder);
        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);
        
        guide.ConfigureEndpoints(context =>
        {
            ConfigureEndpoints(context.ApplicationBuilder);
        });
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
    /// 声明依赖的模块，并进行配置等。注意，在该方法中的Option不一定是最终的Option值，请谨慎在此方法中获取Option值。（目前仅能获取到开发者配置后的Option，无法合并其他模块自动注册期间设置的值）
    /// </summary>
    public void ClaimDependencies();
}
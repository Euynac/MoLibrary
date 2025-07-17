using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Core.Modules;


public static class ModuleControllersBuilderExtensions
{
    public static ModuleControllersGuide ConfigModuleControllers(this WebApplicationBuilder builder,
        Action<ModuleControllersOption>? action = null)
    {
        return new ModuleControllersGuide().Register(action);
    }
}

public class ModuleControllers(ModuleControllersOption option)
    : MoModule<ModuleControllers, ModuleControllersOption, ModuleControllersGuide>(option)
{
    public override void PostConfigureServices(IServiceCollection services)
    {
        var mvcBuilder = services.AddControllers().ConfigureApplicationPartManager(manager =>
            {

            })
            .AddControllersAsServices(); 


        if (Option.MvcBuilderActions.Count <= 0 && Option.MvcOptionActions.Count <= 0 && Option.DependentServicesActions.Count <= 0) return;
        foreach (var action in Option.DependentServicesActions)
        {
            action(services);
        }
        var serviceProvider = services.BuildServiceProvider();
        foreach (var action in Option.MvcBuilderActions)
        {
            action(mvcBuilder, serviceProvider);
        }
        foreach (var action in Option.MvcOptionActions)
        {
            services.Configure<MvcOptions>(o =>
            {
                action(o, serviceProvider);
            });
        }
   
        mvcBuilder.ConfigureApplicationPartManager(manager =>
        {
            // 动态控制Controller的启用
            manager.FeatureProviders.Add(new ConditionalControllerFeatureProvider(Option.EnabledMoModuleControllers));
        });
    }

    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Controllers;
    }
}

public class ModuleControllersGuide : MoModuleGuide<ModuleControllers, ModuleControllersOption, ModuleControllersGuide>
{
    public ModuleControllersGuide ConfigDependentServices(Action<IServiceCollection> action)
    {
        ConfigureModuleOption(o =>
        {
            o.AddDependentServicesAction(action);
        }, secondKey: Guid.NewGuid().ToString());
        return this;
    }
    public ModuleControllersGuide ConfigMvcBuilder(Action<IMvcBuilder, IServiceProvider> action)
    {
        ConfigureModuleOption(o =>
        {
            o.AddMvcBuilderAction(action);
        }, secondKey: Guid.NewGuid().ToString());
        return this;
    }
    public ModuleControllersGuide ConfigMvcOption(Action<MvcOptions, IServiceProvider> action)
    {
        ConfigureModuleOption(o =>
        {
            o.AddMvcOptionAction(action);
        }, secondKey: Guid.NewGuid().ToString());
        return this;
    }
    public ModuleControllersGuide RegisterMoControllers<TController>(IMoModuleControllerOption options) where TController : MoModuleControllerBase
    {
        if (options.DisableControllers)
        {
            Logger.LogWarning("MoModuleController {Controller} is disabled", typeof(TController).Name);
            return this;
        }

        ConfigureModuleOption(o =>
        {
            o.EnabledMoModuleControllers.Add(typeof(TController));
            o.AddMvcOptionAction((mvcOptions, provider) =>
            {
                mvcOptions.Conventions.Add(new ModuleControllerModelConvention<TController>(options));
            });
        }, secondKey: typeof(TController).Name);

        return this;
    }
}

public class ModuleControllersOption : MoModuleOption<ModuleControllers>
{
    internal List<Action<IMvcBuilder, IServiceProvider>> MvcBuilderActions { get; set; } = [];
    internal List<Action<MvcOptions, IServiceProvider>> MvcOptionActions { get; set; } = [];
    internal List<Action<IServiceCollection>> DependentServicesActions { get; set; } = [];
    internal HashSet<Type> EnabledMoModuleControllers { get; set; } = [];
    public void AddMvcBuilderAction(Action<IMvcBuilder, IServiceProvider> action)
    {
        MvcBuilderActions.Add(action);
    }
    public void AddMvcOptionAction(Action<MvcOptions, IServiceProvider> action)
    {
        MvcOptionActions.Add(action);
    }
    public void AddDependentServicesAction(Action<IServiceCollection> action)
    {
        DependentServicesActions.Add(action);
    }
}
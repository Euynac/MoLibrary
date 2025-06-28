using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

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
    public override void ConfigureServices(IServiceCollection services)
    {
        var mvcBuilder = services.AddControllers().ConfigureApplicationPartManager(manager =>
            {

            })
            .AddControllersAsServices();


        if (option.MvcBuilderActions.Count <= 0 && option.MvcOptionActions.Count <= 0 && option.DependentServicesActions.Count <= 0) return;
        foreach (var action in option.DependentServicesActions)
        {
            action(services);
        }
        var serviceProvider = services.BuildServiceProvider();
        foreach (var action in option.MvcBuilderActions)
        {
            action(mvcBuilder, serviceProvider);
        }
        foreach (var action in option.MvcOptionActions)
        {
            services.Configure<MvcOptions>(o =>
            {
                action(o, serviceProvider);
            });
        }
        
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
        });
        return this;
    }
    public ModuleControllersGuide ConfigMvcBuilder(Action<IMvcBuilder, IServiceProvider> action)
    {
        ConfigureModuleOption(o =>
        {
            o.AddMvcBuilderAction(action);
        });
        return this;
    }
    public ModuleControllersGuide ConfigMvcOption(Action<MvcOptions, IServiceProvider> action)
    {
        ConfigureModuleOption(o =>
        {
            o.AddMvcOptionAction(action);
        });
        return this;
    }

}

public class ModuleControllersOption : MoModuleOption<ModuleControllers>
{
    internal List<Action<IMvcBuilder, IServiceProvider>> MvcBuilderActions { get; set; } = [];
    internal List<Action<MvcOptions, IServiceProvider>> MvcOptionActions { get; set; } = [];
    internal List<Action<IServiceCollection>> DependentServicesActions { get; set; } = [];
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
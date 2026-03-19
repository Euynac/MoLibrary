using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleControllersBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Controllers module.
        /// </summary>
        public static ModuleControllersGuide AddControllers(Action<ModuleControllersOption>? action = null)
        {
            return new ModuleControllersGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Controllers)]
public class ModuleControllers(ModuleControllersOption option)
    : MoModule<ModuleControllers, ModuleControllersOption, ModuleControllersGuide>(option)
{
    public override void PostConfigureServices(IServiceCollection services)
    {
        var mvcBuilder = services.AddControllers().ConfigureApplicationPartManager(manager =>
            {

            }); 

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

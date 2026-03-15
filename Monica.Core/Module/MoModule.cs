using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Module.Features;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

namespace Monica.Core.Module;

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

    public abstract ModuleKey GetModuleKey();
    internal abstract void ConvertToRegisterRequest();
}


/// <summary>
/// Base abstract class for Monica modules.
/// Provides the default implementation of <see cref="IMoModule"/>.
/// </summary>
public abstract class MoModule<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option) : MoModule, IMoModuleStaticInfo, IMoModuleGuideBridge
    where TModuleOption : MoModuleOption<TModuleSelf>, new() 
    where TModuleSelf : MoModule<TModuleSelf, TModuleOption, TModuleGuide>
    where TModuleGuide : MoModuleGuide<TModuleSelf, TModuleOption, TModuleGuide>, new()
{
    public TModuleOption Option { get; } = option;
    public ILogger Logger { get;  } = option.Logger;

    /// <summary>
    /// Gets the module key representing this module type.
    /// This static method creates a temporary instance to access the GetModuleKey method.
    /// </summary>
    /// <returns>The ModuleKey representing this module.</returns>
    public static ModuleKey GetStaticModuleKey()
    {
        // Create a temporary instance with default options to get the module key
        var instance = Activator.CreateInstance(typeof(TModuleSelf), new TModuleOption()) as TModuleSelf;
        var moduleKey = instance!.GetModuleKey();

        // Register the mapping between module type and key
        ModuleAnalyser.RegisterModuleMapping(typeof(TModuleSelf), moduleKey);

        return moduleKey;
    }

    /// <summary>
    /// Gets a configured option object for another module.
    /// </summary>
    /// <typeparam name="TSpecificModuleOption">The option type to retrieve.</typeparam>
    /// <returns>The configured option instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the option type cannot be resolved from a registered module.</exception>
    public TSpecificModuleOption GetOptions<TSpecificModuleOption>() where TSpecificModuleOption : IMoModuleOptionBase, new()
    {
        var optionInterface = typeof(TSpecificModuleOption)
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMoModuleOptionBase<>));

        if (optionInterface == null)
            throw new InvalidOperationException($"{typeof(TSpecificModuleOption).Name} does not implement IMoModuleOptionBase<T>.");

        var moduleType = optionInterface.GetGenericArguments()[0];
        MoModuleRegisterCentre.ModuleRegisterContextDict.TryGetValue(moduleType, out var context);
        
        if (context == null)
            throw new InvalidOperationException($"Module {moduleType.Name} is not registered.");

        context.FinalConfigures.TryGetValue(typeof(TSpecificModuleOption), out var value);
        if(value == null)
            throw new InvalidOperationException($"Module {moduleType.Name} does not have option {typeof(TSpecificModuleOption).Name} or is not initialized in current stage.");
        return (TSpecificModuleOption)value;
    }
    internal override void ConvertToRegisterRequest()
    {
        var guide = new TModuleGuide(); // TODO: this path does not currently preserve the original registration source.

        guide.ConfigureBuilder(context =>
        {
            ConfigureBuilder(context.WebApplicationBuilder);
        }, -1);

        guide.ConfigureServices(context =>
        {
            ConfigureServices(context.Services);
        }, -1);

        guide.PostConfigureServices(context =>
        {
            PostConfigureServices(context.Services);
        }, -1);

        guide.ConfigureApplicationBuilder(context =>
        {
            ConfigureApplicationBuilder(context.ApplicationBuilder);
        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);
        
        guide.ConfigureEndpoints(context =>
        {
            ConfigureEndpoints(context.ApplicationBuilder);
        }, -1);
    }
    

    public void CheckRequiredMethod(string methodName, string? errorDetail = null)
    {
        new TModuleGuide().CheckRequiredMethod(methodName, errorDetail);
    }

    protected void UseEndpoints(IApplicationBuilder builder, Action<IEndpointRouteBuilder> configure)
    {
        if (Option is IMoModuleOptionWithMinimalApi option && option.GetIsMinimalApiDisabled())
        {
            return;
        }
        builder.UseEndpoints(configure);
    }
}

public abstract class MoModuleWithDependencies<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option) : MoModule<TModuleSelf, TModuleOption, TModuleGuide>(option), IWantDependsOnOtherModules
    where TModuleOption : MoModuleOption<TModuleSelf>, new()
    where TModuleSelf : MoModuleWithDependencies<TModuleSelf, TModuleOption, TModuleGuide>
    where TModuleGuide : MoModuleGuide<TModuleSelf, TModuleOption, TModuleGuide>, new()
{
    public abstract void ClaimDependencies();


    [MustUseReturnValue]
    protected TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()
        where TOtherModuleGuide : MoModuleGuide, new()
    {
        return MoModuleGuide.DeclareDependency<TOtherModuleGuide>(GetModuleKey(), GetModuleKey());
    }
}

public interface IWantDependsOnOtherModules
{
    /// <summary>
    /// Declares dependent modules and optionally configures them.
    /// The option instance available here may not be final because automatically registered configuration from other modules has not been merged yet.
    /// </summary>
    public void ClaimDependencies();
}

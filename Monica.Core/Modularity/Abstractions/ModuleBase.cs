using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;

namespace Monica.Core.Modularity.Abstractions;

public abstract class ModuleBase : IWebModule
{
    public ModuleKey ModuleKey => ModuleDependencyAnalyzer.ResolveModuleKey(GetType());

    public virtual void ConfigureBuilder(IHostApplicationBuilder builder)
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

    internal abstract void ConvertToRegisterRequest();
}


/// <summary>
/// Base abstract class for Monica modules.
/// Provides the default implementation of <see cref="IWebModule"/>.
/// </summary>
public abstract class ModuleBase<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option) : ModuleBase, IModuleRequirementChecker, IModuleDependencyDeclarer
    where TModuleOption : ModuleOptions<TModuleSelf>, new() 
    where TModuleSelf : ModuleBase<TModuleSelf, TModuleOption, TModuleGuide>
    where TModuleGuide : ModuleGuide<TModuleSelf, TModuleOption, TModuleGuide>, new()
{
    public TModuleOption Option { get; } = option;
    public ILogger Logger { get;  } = option.Logger;

    /// <summary>
    /// Gets a configured option object for another module.
    /// </summary>
    /// <typeparam name="TSpecificModuleOption">The option type to retrieve.</typeparam>
    /// <returns>The configured option instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the option type cannot be resolved from a registered module.</exception>
    public TSpecificModuleOption GetOptions<TSpecificModuleOption>() where TSpecificModuleOption : IModuleOptionsBase, new()
    {
        var optionInterface = typeof(TSpecificModuleOption)
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IModuleOptionsBase<>));

        if (optionInterface == null)
            throw new InvalidOperationException($"{typeof(TSpecificModuleOption).Name} does not implement IModuleOptionsBase<T>.");

        var moduleType = optionInterface.GetGenericArguments()[0];
        ModuleRegistry.ModuleRegisterContextDict.TryGetValue(moduleType, out var context);
        
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
            ConfigureBuilder(context.HostApplicationBuilder);
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
        }, ModuleApplicationMiddlewareOrder.BeforeUseRouting);
        
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
        if (Option is IMinimalApiModuleOptions option && option.GetIsMinimalApiDisabled())
        {
            return;
        }
        builder.UseEndpoints(configure);
    }
    public virtual void ClaimDependencies()
    {
    }


    [MustUseReturnValue]
    protected TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()
        where TOtherModuleGuide : ModuleGuide, new()
    {
        return ModuleGuide.DeclareDependency<TOtherModuleGuide>(ModuleKey, ModuleKey);
    }
}


public interface IModuleDependencyDeclarer
{
    /// <summary>
    /// Declares dependent modules and optionally configures them.
    /// The option instance available here may not be final because automatically registered configuration from other modules has not been merged yet.
    /// </summary>
    void ClaimDependencies();
}

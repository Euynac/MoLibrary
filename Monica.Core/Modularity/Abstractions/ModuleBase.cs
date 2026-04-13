using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Base type for Monica modules that participate only in the host-builder and service-registration lifecycle.
/// </summary>
public abstract class ModuleBase : IModule
{
    /// <summary>
    /// Gets the resolved module key declared on the concrete module type.
    /// </summary>
    public ModuleKey ModuleKey => ModuleDependencyAnalyzer.ResolveModuleKey(GetType());

    /// <summary>
    /// Configures the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    public virtual void ConfigureBuilder(IHostApplicationBuilder builder)
    {
    }

    /// <summary>
    /// Configures service registrations for the module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Configures post-service registration actions after business-type iteration finishes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public virtual void PostConfigureServices(IServiceCollection services)
    {
    }

    internal abstract void ConvertToRegisterRequest();
}


/// <summary>
/// Base abstract class for Monica modules.
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
    }

    public void CheckRequiredMethod(string methodName, string? errorDetail = null)
    {
        new TModuleGuide().CheckRequiredMethod(methodName, errorDetail);
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

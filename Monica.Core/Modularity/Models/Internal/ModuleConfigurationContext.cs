using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Abstractions;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Module registration context used while configuring services, builders, and endpoints.
/// </summary>
/// <param name="services">The service collection.</param>
/// <param name="applicationBuilder">The application builder.</param>
/// <param name="hostApplicationBuilder">The host application builder.</param>
/// <param name="moduleRegisterInfo">The module registration information.</param>
public class ModuleConfigurationContext(IServiceCollection? services, IApplicationBuilder? applicationBuilder, IHostApplicationBuilder? hostApplicationBuilder, ModuleRegistrationState moduleRegisterInfo)
{
    /// <summary>
    /// The service collection.
    /// </summary>
    public IServiceCollection? Services { get; init; } = services;

    /// <summary>
    /// The application builder.
    /// </summary>
    public IApplicationBuilder? ApplicationBuilder { get; init; } = applicationBuilder;

    /// <summary>
    /// The host application builder.
    /// </summary>
    public IHostApplicationBuilder? HostApplicationBuilder { get; init; } = hostApplicationBuilder;
    
    /// <summary>
    /// The module registration information.
    /// </summary>
    public ModuleRegistrationState ModuleRegisterInfo { get; init; } = moduleRegisterInfo;

    /// <summary>
    /// Finalized option objects for the current module.
    /// </summary>
    internal Dictionary<Type, object> Option  => ModuleRegisterInfo.FinalConfigures;
}

/// <summary>
/// Generic module registration context that exposes strongly typed module options.
/// </summary>
/// <typeparam name="TModuleOption">The module option type.</typeparam>
public class ModuleConfigurationContext<TModuleOption>(ModuleConfigurationContext context)  where TModuleOption : IModuleOptions
{
    protected ModuleConfigurationContext Context { get; init; } = context;
    /// <summary>
    /// Gets the current module option.
    /// </summary>
    public TModuleOption ModuleOption => (TModuleOption) Context.Option[typeof(TModuleOption)];
    
    /// <summary>
    /// Gets an extra module option, creating a new instance when one is not configured.
    /// </summary>
    /// <typeparam name="TModuleExtraOption">The extra option type.</typeparam>
    /// <returns>The extra option instance.</returns>
    public TModuleExtraOption GetModuleExtraOption<TModuleExtraOption>() where TModuleExtraOption : IModuleOptionsBase, new()
    {
        return GetModuleExtraOptionOrDefault<TModuleExtraOption>() ?? new TModuleExtraOption();
    }
    
    /// <summary>
    /// Gets an extra module option or returns the default value when it is not configured.
    /// </summary>
    /// <typeparam name="TModuleExtraOption">The extra option type.</typeparam>
    /// <returns>The configured extra option, or the default value.</returns>
    public TModuleExtraOption? GetModuleExtraOptionOrDefault<TModuleExtraOption>() where TModuleExtraOption : IModuleOptionsBase, new()
    {
        if (Context.Option.TryGetValue(typeof(TModuleExtraOption), out var option))
        {
            return (TModuleExtraOption) option;
        }

        return default;
    }
}

public class ModuleServiceConfigurationContext<TModuleOption>(ModuleConfigurationContext context) : ModuleConfigurationContext<TModuleOption>(context) where TModuleOption : IModuleOptions
{
    public IServiceCollection Services => Context.Services!;
}

public class ModuleApplicationConfigurationContext<TModuleOption>(ModuleConfigurationContext context) : ModuleConfigurationContext<TModuleOption>(context) where TModuleOption : IModuleOptions
{
    public IApplicationBuilder ApplicationBuilder => Context.ApplicationBuilder!;

    /// <summary>
    /// Gets the current application builder as a <see cref="WebApplication"/>.
    /// </summary>
    /// <returns>The current <see cref="WebApplication"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the current application builder is not a <see cref="WebApplication"/>.</exception>
    public WebApplication RequireWebApplication()
    {
        return ApplicationBuilder as WebApplication
               ?? throw new InvalidOperationException(
                   $"{Context.ModuleRegisterInfo.ModuleType.Name} requires {nameof(WebApplication)} during {Context.ModuleRegisterInfo.ModulePhase}.");
    }
}

public class ModuleBuilderConfigurationContext<TModuleOption>(ModuleConfigurationContext context) : ModuleConfigurationContext<TModuleOption>(context) where TModuleOption : IModuleOptions
{
    public IHostApplicationBuilder HostApplicationBuilder => Context.HostApplicationBuilder!;
}



public class ModuleConfigurationRequest(string key)
{
    public Action<ModuleConfigurationContext>? ConfigureContext { get; set; }
    /// <summary>
    /// Configuration key. Configurations with the same Key are executed only once during the execution phase (first occurrence wins).
    /// </summary>
    public string Key { get; set; } = key;
    /// <summary>
    /// Source module that issued the request. `null` means direct developer configuration.
    /// </summary>
    public ModuleKey? RequestFrom { get; set; }
    public ModulePhase? RequestMethod { get; set; }
    public int Order { get; set; }
    /// <summary>
    /// Additional description of the request source.
    /// </summary>
    public string? SourceDesc { get; set; }

    public override string ToString()
    {
        return Key;
    }
}

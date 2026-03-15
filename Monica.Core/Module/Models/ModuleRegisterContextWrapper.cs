using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module.Interfaces;
using Monica.Tool.Extensions;

namespace Monica.Core.Module.Models;

/// <summary>
/// Module registration context used while configuring services, builders, and endpoints.
/// </summary>
/// <param name="services">The service collection.</param>
/// <param name="applicationBuilder">The application builder.</param>
/// <param name="webApplicationBuilder">The web application builder.</param>
/// <param name="moduleRegisterInfo">The module registration information.</param>
public class ModuleRegisterContext(IServiceCollection? services, IApplicationBuilder? applicationBuilder, WebApplicationBuilder? webApplicationBuilder, ModuleRegisterInfo moduleRegisterInfo)
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
    /// Gets the application as a <see cref="WebApplication"/> when the current builder supports it.
    /// </summary>
    public WebApplication? WebApplication => ApplicationBuilder == null ? null : ApplicationBuilder as WebApplication ?? throw new InvalidOperationException($"当前{nameof(ApplicationBuilder)}是{ApplicationBuilder.GetType().GetCleanFullName()}类型，而不是{nameof(WebApplication)}类型！");
    
    /// <summary>
    /// The web application builder.
    /// </summary>
    public WebApplicationBuilder? WebApplicationBuilder { get; init; } = webApplicationBuilder;
    
    /// <summary>
    /// The module registration information.
    /// </summary>
    public ModuleRegisterInfo ModuleRegisterInfo { get; init; } = moduleRegisterInfo;

    /// <summary>
    /// Finalized option objects for the current module.
    /// </summary>
    internal Dictionary<Type, object> Option  => ModuleRegisterInfo.FinalConfigures;
}

/// <summary>
/// Generic module registration context that exposes strongly typed module options.
/// </summary>
/// <typeparam name="TModuleOption">The module option type.</typeparam>
public class ModuleRegisterContextWrapper<TModuleOption>(ModuleRegisterContext context)  where TModuleOption : IMoModuleOption
{
    protected ModuleRegisterContext Context { get; init; } = context;
    /// <summary>
    /// Gets the current module option.
    /// </summary>
    public TModuleOption ModuleOption => (TModuleOption) Context.Option[typeof(TModuleOption)];
    
    /// <summary>
    /// Gets an extra module option, creating a new instance when one is not configured.
    /// </summary>
    /// <typeparam name="TModuleExtraOption">The extra option type.</typeparam>
    /// <returns>The extra option instance.</returns>
    public TModuleExtraOption GetModuleExtraOption<TModuleExtraOption>() where TModuleExtraOption : IMoModuleOptionBase, new()
    {
        return GetModuleExtraOptionOrDefault<TModuleExtraOption>() ?? new TModuleExtraOption();
    }
    
    /// <summary>
    /// Gets an extra module option or returns the default value when it is not configured.
    /// </summary>
    /// <typeparam name="TModuleExtraOption">The extra option type.</typeparam>
    /// <returns>The configured extra option, or the default value.</returns>
    public TModuleExtraOption? GetModuleExtraOptionOrDefault<TModuleExtraOption>() where TModuleExtraOption : IMoModuleOptionBase, new()
    {
        if (Context.Option.TryGetValue(typeof(TModuleExtraOption), out var option))
        {
            return (TModuleExtraOption) option;
        }

        return default;
    }
}

public class ModuleRegisterContextWrapperForServices<TModuleOption>(ModuleRegisterContext context) : ModuleRegisterContextWrapper<TModuleOption>(context) where TModuleOption : IMoModuleOption
{
    public IServiceCollection Services => Context.Services!;
}

public class ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>(ModuleRegisterContext context) : ModuleRegisterContextWrapper<TModuleOption>(context) where TModuleOption : IMoModuleOption
{
    public IApplicationBuilder ApplicationBuilder => Context.ApplicationBuilder!;
    public WebApplication WebApplication => Context.WebApplication!;
}

public class ModuleRegisterContextWrapperForBuilder<TModuleOption>(ModuleRegisterContext context) : ModuleRegisterContextWrapper<TModuleOption>(context) where TModuleOption : IMoModuleOption
{
    public WebApplicationBuilder WebApplicationBuilder => Context.WebApplicationBuilder!;
}



public class ModuleRegisterRequest(string key)
{
    public Action<ModuleRegisterContext>? ConfigureContext { get; set; }
    /// <summary>
    /// Configuration key. Configurations with the same Key are executed only once during the execution phase (first occurrence wins).
    /// </summary>
    public string Key { get; set; } = key;
    /// <summary>
    /// Source module that issued the request. `null` means direct developer configuration.
    /// </summary>
    public ModuleKey? RequestFrom { get; set; }
    public EMoModuleConfigMethods? RequestMethod { get; set; }
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

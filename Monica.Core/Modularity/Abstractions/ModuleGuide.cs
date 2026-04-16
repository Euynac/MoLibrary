using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Core.Modularity.Abstractions;
public class ModuleGuide
{
    /// <summary>
    /// Indicates where this module configuration originated. `null` means the developer configured it directly.
    /// </summary>
    public ModuleKey? GuideFrom { get; set; }

    /// <summary>
    /// Lazy-loaded logger instance for this module guide.
    /// </summary>
    private readonly Lazy<ILogger> _loggerLazy;

    /// <summary>
    /// Gets the logger instance for this module guide.
    /// </summary>
    public ILogger Logger => _loggerLazy.Value;

    public ModuleGuide()
    {
        GuideFrom = null; // null means direct developer configuration
        _loggerLazy = new Lazy<ILogger>(() => LogManager.For(GetType()));
    }

    /// <summary>
    /// Gets the target module key this guide is for.
    /// </summary>
    /// <returns>The ModuleKey representation of the target module.</returns>
    public virtual ModuleKey GetTargetModuleKey()
    {
        // Default implementation returns a placeholder value
        // This should be overridden in specific module guides
        return default;
    }

    /// <summary>
    /// Declares a dependency on another module from given module and returns a guide for configuring that module.
    /// </summary>
    /// <typeparam name="TDependsModuleGuide">Type of the module guide for the dependent module.</typeparam>
    /// <returns>A module guide for configuring the dependent module.</returns>
    internal static TDependsModuleGuide DeclareDependency<TDependsModuleGuide>(ModuleKey fromModule, ModuleKey? guideFrom)
        where TDependsModuleGuide : ModuleGuide, new()
    {
        // Add dependency to the list if it's not already there
        var dependsOnModule = new TDependsModuleGuide().GetTargetModuleKey();

        // Register this dependency relationship in the ModuleDependencyAnalyzer
        ModuleDependencyAnalyzer.AddDependency(fromModule, dependsOnModule);

        return new TDependsModuleGuide()
        {
            GuideFrom = guideFrom
        };
    }

    /// <summary>
    /// Declares a dependency on another module and returns a guide for configuring that module.
    /// </summary>
    /// <typeparam name="TOtherModuleGuide">Type of the module guide for the dependent module.</typeparam>
    /// <returns>A module guide for configuring the dependent module.</returns>
    public TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()
        where TOtherModuleGuide : ModuleGuide, new()
    {
        return DeclareDependency<TOtherModuleGuide>(GetTargetModuleKey(), GetTargetModuleKey());
    }
}
public class ModuleGuide<TModule, TModuleOption, TModuleGuideSelf> : ModuleGuide, IModuleGuide, IModuleRequirementChecker
    where TModuleOption : ModuleOptions<TModule>, new()
    where TModuleGuideSelf : ModuleGuide<TModule, TModuleOption, TModuleGuideSelf>, new()
    where TModule : ModuleBase<TModule, TModuleOption, TModuleGuideSelf>
{
    public override ModuleKey GetTargetModuleKey()
    {
        return ModuleDependencyAnalyzer.ResolveModuleKey(typeof(TModule));
    }

    /// <summary>
    /// Returns the configuration methods that must be called manually for this module.
    /// </summary>
    /// <returns>The required configuration method keys.</returns>
    protected virtual string[] GetRequestedConfigMethodKeys()
    {
        return Array.Empty<string>();
    }
    
  

   
    #region Registration

    /// <summary>
    /// Registers the module type and returns its registration information.
    /// </summary>
    /// <returns>The module registration information.</returns>
    private ModuleRegistrationState RegisterModule()
    {
        var moduleType = typeof(TModule);
        ModuleDependencyAnalyzer.ResolveModuleKey(moduleType);
        if (ModuleRegistry.TryGetModuleRequestInfo(moduleType, out var requestInfo)) return requestInfo;

        requestInfo = new ModuleRegistrationState(moduleType);
        requestInfo.BindModuleOption<TModuleOption>();
        ModuleRegistry.AddModuleRegisterContext(moduleType, requestInfo);
        // Record configuration methods that must be provided explicitly.
        requestInfo.RequiredConfigMethodKeys = GetRequestedConfigMethodKeys().ToList();

        return requestInfo;
    }

    /// <summary>
    /// Registers the module and appends a registration request.
    /// </summary>
    /// <param name="request">The registration request.</param>
    public void RegisterModule(ModuleConfigurationRequest request)
    {
        var actions = RegisterModule().RegisterRequests;
        actions.Add(request);
    }

    
    #endregion


    public void CheckRequiredMethod(string methodName, string? errorDetail = null)
    {
        // TODO: Validate that the specified required method has been configured and throw if it has not.
        
    }

    /// <summary>
    /// Issues a module registration request.
    /// </summary>
    /// <param name="config">The module configuration action.</param>
    /// <returns>The current guide instance.</returns>
    public TModuleGuideSelf Register(Action<TModuleOption>? config = null)
    {
        if (config != null)
        {
            ConfigureModuleOption(config);
        }

        RegisterModule();

        return (TModuleGuideSelf)this;
    }

    /// <summary>
    /// Records a keyed service key exposed by the module so it can be discovered later.
    /// </summary>
    /// <param name="serviceKey">The keyed service key.</param>
    /// <returns>The current guide instance.</returns>
    public TModuleGuideSelf RecordKeyedServiceKey(string serviceKey)
    {
        var requestInfo = RegisterModule();
        requestInfo.KeyedServiceKeys.Add(serviceKey);
        return (TModuleGuideSelf)this;
    }

    #region Additional Module Configuration

    /// <summary>
    /// Core configuration method that records module registration requests.
    /// </summary>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="context">The module registration context action.</param>
    /// <param name="order">The execution order.</param>
    /// <param name="requestMethod">The configuration method being requested.</param>
    protected internal void ConfigureModule(string key, string? secondKey, Action<ModuleConfigurationContext> context, int order,
        ModulePhase requestMethod)
    {
        var request = new ModuleConfigurationRequest($"{key}{secondKey?.BeAfter("_")}")
        {
            Order = order,
            RequestFrom = GuideFrom,
            RequestMethod = requestMethod,
            ConfigureContext = context
        };
        RegisterModule(request);
    }

    /// <summary>
    /// Records an empty registration so a configuration method call is still tracked.
    /// </summary>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureEmpty([CallerMemberName] string key = "")
    {
        RegisterModule(new ModuleConfigurationRequest(key)
        {
            RequestFrom = GuideFrom
        });
    }

    /// <summary>
    /// Configures the module service registrations.
    /// </summary>
    /// <param name="context">The service configuration context action.</param>
    /// <param name="order">The concrete execution order value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureServices(Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        int order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleServiceConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.ConfigureServices);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureServices(System.Action{ModuleServiceConfigurationContext{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">The service configuration context action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureServices(Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureServices(context, (int)order, secondKey, key);
    }

    /// <summary>
    /// Configures post-service registration actions for the module.
    /// </summary>
    /// <param name="context">The post-service configuration context action.</param>
    /// <param name="order">The concrete execution order value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void PostConfigureServices(
        Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        int order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleServiceConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.PostConfigureServices);
    }

    /// <summary>
    /// <inheritdoc cref="PostConfigureServices(System.Action{ModuleServiceConfigurationContext{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">The post-service configuration context action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void PostConfigureServices(
        Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        PostConfigureServices(context, (int)order, secondKey, key);
    }

    /// <summary>
    /// Configures the <see cref="Microsoft.Extensions.Hosting.IHostApplicationBuilder"/> for the module.
    /// </summary>
    /// <param name="context">The builder configuration context action.</param>
    /// <param name="order">The concrete execution order value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureBuilder(Action<ModuleBuilderConfigurationContext<TModuleOption>> context,
        int order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleBuilderConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.ConfigureBuilder);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureBuilder(System.Action{ModuleBuilderConfigurationContext{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">The builder configuration context action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    protected internal void ConfigureBuilder(Action<ModuleBuilderConfigurationContext<TModuleOption>> context,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureBuilder(context, (int)order, secondKey, key);
    }

    #endregion

    #region Additional Settings

    /// <summary>
    /// Configures the module options.
    /// </summary>
    /// <param name="optionAction">The module option configuration action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    public TModuleGuideSelf ConfigureModuleOption(Action<TModuleOption>? optionAction,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        return ConfigureOption(optionAction, (int) order, secondKey, key);
    }

    /// <summary>
    /// Applies option configuration for the specified option type.
    /// </summary>
    /// <param name="optionAction">The option configuration action.</param>
    /// <param name="order">The execution order value.</param>
    /// <param name="secondKey">The optional secondary configuration key.</param>
    /// <param name="key">The unique configuration method key.</param>
    private TModuleGuideSelf ConfigureOption<TOption>(Action<TOption>? optionAction, int order, string? secondKey,
        string key) where TOption : class, IModuleOptionsBase, new()
    {
        if(optionAction == null) return (TModuleGuideSelf) this;

        // Cascaded dependency configuration should be deduplicated per requester, not globally by method name.
        secondKey ??= GuideFrom?.ToString();

        var requestInfo = RegisterModule();
        requestInfo.AddConfigureAction(order, optionAction, GuideFrom, secondKey, key);
        return (TModuleGuideSelf) this;
    }

    /// <summary>
    /// Configures an extra option object for the module.
    /// </summary>
    /// <param name="optionAction">The extra option configuration action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">The optional secondary configuration key.</param>
    /// <param name="key">The unique configuration method key.</param>
    public TModuleGuideSelf ConfigureExtraOption<TOption>(Action<TOption>? optionAction,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "") where TOption : class, IModuleExtraOptions<TModule>, new()
    {
        return ConfigureOption(optionAction, (int) order, secondKey, key);
    }

    #endregion

}

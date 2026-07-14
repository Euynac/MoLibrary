using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Tool.Extensions;

namespace Monica.Core.Modularity.Abstractions;
public class ModuleGuide
{
    private MonicaApplication? _application;

    /// <summary>
    /// Indicates where this module configuration originated. `null` means the developer configured it directly.
    /// </summary>
    public ModuleKey? GuideFrom { get; private set; }

    /// <summary>
    /// Gets the logger instance for this module guide.
    /// </summary>
    public ILogger Logger => Application.CreateLogger(GetType());

    /// <summary>
    /// Gets the host-bound Monica application that owns this guide.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a guide was constructed outside a Monica builder.</exception>
    protected MonicaApplication Application =>
        _application
        ?? throw new InvalidOperationException(
            $"{GetType().Name} is not bound to an IMonicaBuilder. Register modules inside AddMonica(...).");

    /// <summary>
    /// Binds this guide to one host composition context.
    /// </summary>
    /// <param name="application">The owning Monica application.</param>
    /// <param name="configuredBy">The module that requested this guide, or <see langword="null"/> for direct registration.</param>
    internal void Bind(MonicaApplication application, ModuleKey? configuredBy)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null && !ReferenceEquals(_application, application))
        {
            throw new InvalidOperationException($"{GetType().Name} is already bound to another Monica host.");
        }

        _application = application;
        GuideFrom = configuredBy;
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
    internal TDependsModuleGuide DeclareDependency<TDependsModuleGuide>(ModuleKey fromModule, ModuleKey? guideFrom)
        where TDependsModuleGuide : ModuleGuide, new()
    {
        var dependencyGuide = Application.CreateGuide<TDependsModuleGuide>(guideFrom);
        Application.Dependencies.AddDependency(fromModule, dependencyGuide.GetTargetModuleKey());
        return dependencyGuide;
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

    /// <summary>
    /// Registers a companion module in the same host composition without declaring a dependency edge from this module.
    /// </summary>
    /// <typeparam name="TModule">The companion module type.</typeparam>
    /// <typeparam name="TModuleOption">The companion module option type.</typeparam>
    /// <typeparam name="TModuleGuide">The companion module guide type.</typeparam>
    /// <param name="configure">An optional module option callback.</param>
    /// <returns>A guide bound to the same host composition.</returns>
    public TModuleGuide AddModule<TModule, TModuleOption, TModuleGuide>(
        Action<TModuleOption>? configure = null)
        where TModuleOption : ModuleOptions<TModule>, new()
        where TModuleGuide : ModuleGuide<TModule, TModuleOption, TModuleGuide>, new()
        where TModule : ModuleBase<TModule, TModuleOption, TModuleGuide>
    {
        return Application.CreateGuide<TModuleGuide>().Register(configure);
    }
}
public class ModuleGuide<TModule, TModuleOption, TModuleGuideSelf> : ModuleGuide, IModuleGuide, IModuleRequirementChecker
    where TModuleOption : ModuleOptions<TModule>, new()
    where TModuleGuideSelf : ModuleGuide<TModule, TModuleOption, TModuleGuideSelf>, new()
    where TModule : ModuleBase<TModule, TModuleOption, TModuleGuideSelf>
{
    public override ModuleKey GetTargetModuleKey()
    {
        return Application.Dependencies.ResolveModuleKey(typeof(TModule));
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
        Application.Modules.EnsureCompositionIsOpen();
        var moduleType = typeof(TModule);
        Application.Dependencies.ResolveModuleKey(moduleType);
        if (Application.Modules.TryGetModuleRequestInfo(moduleType, out var requestInfo)) return requestInfo;

        requestInfo = new ModuleRegistrationState(Application, moduleType);
        requestInfo.BindModuleOption<TModuleOption>();
        Application.Modules.AddModuleRegisterContext(moduleType, requestInfo);
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
    /// <param name="duplicateBehavior">How duplicate requests with the same execution identity should be handled.</param>
    /// <param name="slot">The execution slot used for deduplication.</param>
    protected internal void ConfigureModule(string key, string? secondKey, Action<ModuleConfigurationContext> context, int order,
        ModulePhase requestMethod,
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn,
        ModuleConfigurationRequestSlot slot = ModuleConfigurationRequestSlot.Execution)
    {
        var request = new ModuleConfigurationRequest($"{key}{secondKey?.BeAfter("_")}")
        {
            Order = order,
            RequestFrom = GuideFrom,
            RequestMethod = requestMethod,
            ConfigureContext = context,
            DuplicateBehavior = duplicateBehavior,
            Slot = slot
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
    /// <param name="duplicateBehavior">How duplicate service registration requests with the same execution identity should be handled.</param>
    protected internal void ConfigureServices(Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        int order,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn)
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleServiceConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.ConfigureServices, duplicateBehavior);
    }

    /// <summary>
    /// Configures the module service registrations.
    /// </summary>
    /// <param name="context">The service configuration context action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="duplicateBehavior">How duplicate service registration requests with the same execution identity should be handled.</param>
    protected internal void ConfigureServices(Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn)
    {
        ConfigureServices(context, (int)order, secondKey, key, duplicateBehavior);
    }

    /// <summary>
    /// Configures post-service registration actions for the module.
    /// </summary>
    /// <param name="context">The post-service configuration context action.</param>
    /// <param name="order">The concrete execution order value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="duplicateBehavior">How duplicate post-service requests with the same execution identity should be handled.</param>
    protected internal void PostConfigureServices(
        Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        int order,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn)
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleServiceConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.PostConfigureServices, duplicateBehavior);
    }

    /// <summary>
    /// Configures post-service registration actions for the module.
    /// </summary>
    /// <param name="context">The post-service configuration context action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="duplicateBehavior">How duplicate post-service requests with the same execution identity should be handled.</param>
    protected internal void PostConfigureServices(
        Action<ModuleServiceConfigurationContext<TModuleOption>> context,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn)
    {
        PostConfigureServices(context, (int)order, secondKey, key, duplicateBehavior);
    }

    /// <summary>
    /// Configures the <see cref="Microsoft.Extensions.Hosting.IHostApplicationBuilder"/> for the module.
    /// </summary>
    /// <param name="context">The builder configuration context action.</param>
    /// <param name="order">The concrete execution order value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="duplicateBehavior">How duplicate host-builder requests with the same execution identity should be handled.</param>
    protected internal void ConfigureBuilder(Action<ModuleBuilderConfigurationContext<TModuleOption>> context,
        int order,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn)
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleBuilderConfigurationContext<TModuleOption>(registerContext));
        }, order, ModulePhase.ConfigureBuilder, duplicateBehavior);
    }

    /// <summary>
    /// Configures the <see cref="Microsoft.Extensions.Hosting.IHostApplicationBuilder"/> for the module.
    /// </summary>
    /// <param name="context">The builder configuration context action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">A secondary key for methods that may be invoked multiple times. Use <see cref="Guid.NewGuid()"/> when repeated calls are valid.</param>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="duplicateBehavior">How duplicate host-builder requests with the same execution identity should be handled.</param>
    protected internal void ConfigureBuilder(Action<ModuleBuilderConfigurationContext<TModuleOption>> context,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn)
    {
        ConfigureBuilder(context, (int)order, secondKey, key, duplicateBehavior);
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
    /// <param name="duplicateBehavior">How duplicate option configuration requests with the same execution identity should be handled.</param>
    public TModuleGuideSelf ConfigureModuleOption(Action<TModuleOption>? optionAction,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn)
    {
        return ConfigureOption(optionAction, (int) order, secondKey, key, duplicateBehavior);
    }

    /// <summary>
    /// Applies option configuration for the specified option type.
    /// </summary>
    /// <param name="optionAction">The option configuration action.</param>
    /// <param name="order">The execution order value.</param>
    /// <param name="secondKey">The optional secondary configuration key.</param>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="duplicateBehavior">How duplicate option configuration requests with the same execution identity should be handled.</param>
    private TModuleGuideSelf ConfigureOption<TOption>(
        Action<TOption>? optionAction,
        int order,
        string? secondKey,
        string key,
        ModuleConfigurationDuplicateBehavior duplicateBehavior) where TOption : class, IModuleOptionsBase, new()
    {
        if(optionAction == null) return (TModuleGuideSelf) this;

        // Cascaded dependency configuration should be deduplicated per requester, not globally by method name.
        secondKey ??= GuideFrom?.ToString();

        var requestInfo = RegisterModule();
        requestInfo.AddConfigureAction(order, optionAction, GuideFrom, secondKey, key, duplicateBehavior);
        return (TModuleGuideSelf) this;
    }

    /// <summary>
    /// Configures an extra option object for the module.
    /// </summary>
    /// <param name="optionAction">The extra option configuration action.</param>
    /// <param name="order">The execution order enum value.</param>
    /// <param name="secondKey">The optional secondary configuration key.</param>
    /// <param name="key">The unique configuration method key.</param>
    /// <param name="duplicateBehavior">How duplicate extra-option configuration requests with the same execution identity should be handled.</param>
    public TModuleGuideSelf ConfigureExtraOption<TOption>(Action<TOption>? optionAction,
        ModuleRegistrationOrder order = ModuleRegistrationOrder.Normal,
        string? secondKey = null,
        [CallerMemberName] string key = "",
        ModuleConfigurationDuplicateBehavior duplicateBehavior = ModuleConfigurationDuplicateBehavior.Warn) where TOption : class, IModuleExtraOptions<TModule>, new()
    {
        RegisterModule().DeclareExtraOption<TOption>();
        return ConfigureOption(optionAction, (int) order, secondKey, key, duplicateBehavior);
    }

    #endregion

}

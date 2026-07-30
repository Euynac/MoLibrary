using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Stores module registration requests and configuration data.
/// </summary>
public class ModuleRegistrationState(MonicaApplication application, Type moduleType)
{
    public Type ModuleType { get; } = moduleType;

    /// <summary>
    /// The current registration phase for the module.
    /// </summary>
    public ModulePhase ModulePhase { get; set; }
    
    /// <summary>
    /// Registration order for the module. Lower values are registered first to honor dependencies.
    /// </summary>
    public int Order { get; set; } = 1000; // Default to 1000 so dependency-based ordering can move modules earlier.
    
    /// <summary>
    /// The list of registration requests for the module.
    /// </summary>
    public List<ModuleConfigurationRequest> RegisterRequests { get; set; } = [];

    /// <summary>
    /// Pending configuration actions grouped by option type and ordered by execution priority.
    /// </summary>
    private Dictionary<Type, SortedList<int, Action<object>>> PendingConfigActions { get; } = [];

    /// <summary>
    /// Option types declared by this module, including extra options that use only their defaults.
    /// </summary>
    private HashSet<Type> DeclaredOptionTypes { get; } = [];

    /// <summary>
    /// Finalized configuration objects indexed by option type.
    /// </summary>
    public Dictionary<Type, object> FinalConfigures { get; set; } = [];

    /// <summary>
    /// The primary module option type.
    /// </summary>
    public Type ModuleOptionType { get; set; } = null!;

    /// <summary>
    /// The finalized module option instance.
    /// Available after configuration initialization completes.
    /// </summary>
    public IModuleOptions ModuleOption => (IModuleOptions)FinalConfigures[ModuleOptionType];
    
    /// <summary>
    /// Required configuration method keys that must be provided.
    /// </summary>
    public List<string> RequiredConfigMethodKeys { get; set; } = [];

    /// <summary>
    /// Keyed service keys exposed by the module for later discovery.
    /// </summary>
    public HashSet<string> KeyedServiceKeys { get; } = [];

    /// <summary>
    /// The module singleton created during final configuration initialization.
    /// </summary>
    public ModuleBase? ModuleSingleton { get; internal set; }

    /// <summary>
    /// Indicates whether a web module is currently running in downgraded non-web mode.
    /// </summary>
    public bool IsDowngradedFromWebModule { get; internal set; }

    public void SetModulePhase(ModulePhase phase)
    {
        ModulePhase = phase;
    }

    public void StartModulePhase(ModulePhase phase)
    {
        application.Profiling.StartModulePhase(
            ModuleType,
            application.Dependencies.ResolveModuleKey(ModuleType),
            Order,
            phase);
        SetModulePhase(phase);
    }

    public void EndModulePhase(ModulePhase phase)
    {
        application.Profiling.StopModulePhase(ModuleType, phase);
    }

    /// <summary>
    /// Creates a module option instance based on the configuration actions known so far.
    /// Intended only for exceptional cases during early registration.
    /// </summary>
    /// <returns>The current module option instance.</returns>
    public object CreateCurrentModuleOption()
    {
        var currentModuleOption = CreateOption(ModuleOptionType);
        
        if(!PendingConfigActions.TryGetValue(ModuleOptionType, out var value))
        {
            return currentModuleOption;
        }

        foreach (var action in value.Values)
        {
            action.Invoke(currentModuleOption);
        }

        return currentModuleOption;
    }

    /// <summary>
    /// Initializes the final configuration instances by applying the sorted configuration actions
    /// and then clears the pending configuration actions.
    /// </summary>
    public void InitFinalConfigures()
    {
        foreach (var configType in DeclaredOptionTypes)
        {
            // Create an instance for the configuration type.
            var configInstance = CreateOption(configType);

            if (PendingConfigActions.TryGetValue(configType, out var sortedActions))
            {
                // Apply each configuration action in order.
                foreach (var action in sortedActions.Values)
                {
                    action.Invoke(configInstance);
                }
            }

            // Persist the finalized configuration instance.
            FinalConfigures[configType] = configInstance;
        }

        if (Activator.CreateInstance(ModuleType, ModuleOption) is ModuleBase instance)
        {
            instance.Bind(application);
            instance.ConvertToRegisterRequest();
            ModuleSingleton = instance;
        }

        if (ModuleSingleton == null)
        {
            throw new Exception(
                $"Failed to initialize final configuration for module '{ModuleType.GetCleanFullName()}': the module singleton could not be created.");
        }


        // Clear the pending configuration actions once finalization is complete.
        PendingConfigActions.Clear();
    }

    private object CreateOption(Type optionType)
    {
        var option = Activator.CreateInstance(optionType)
            ?? throw new InvalidOperationException($"Could not create module option {optionType.GetCleanFullName()}.");

        if (option is IModuleOptionsContext context)
        {
            context.Bind(application);
        }

        return option;
    }

    /// <summary>
    /// Gets a finalized option or materializes its default value when the owning module declared no configuration action.
    /// </summary>
    /// <typeparam name="TOption">The option type owned by this module.</typeparam>
    /// <returns>The finalized configured or default option instance.</returns>
    internal TOption GetOrCreateFinalOption<TOption>() where TOption : IModuleOptionsBase, new()
    {
        if (FinalConfigures.TryGetValue(typeof(TOption), out var configuredOption))
        {
            return (TOption)configuredOption;
        }

        if (ModulePhase < ModulePhase.InitFinalConfigures)
        {
            throw new InvalidOperationException(
                $"Module {ModuleType.Name} has not finalized option {typeof(TOption).Name}.");
        }

        var defaultOption = (TOption)CreateOption(typeof(TOption));
        FinalConfigures.Add(typeof(TOption), defaultOption);
        return defaultOption;
    }

    /// <summary>
    /// Binds the primary module option type.
    /// </summary>
    /// <typeparam name="TOption">The module option type.</typeparam>
    public void BindModuleOption<TOption>() where TOption : class, IModuleOptions, new()
    {
        ModuleOptionType = typeof(TOption);
        DeclaredOptionTypes.Add(ModuleOptionType);
    }

    /// <summary>
    /// Declares an extra option type so its default instance is finalized even when no configuration callback is supplied.
    /// </summary>
    /// <typeparam name="TOption">The extra option type owned by this module.</typeparam>
    internal void DeclareExtraOption<TOption>() where TOption : class, IModuleOptionsBase, new()
    {
        DeclaredOptionTypes.Add(typeof(TOption));
    }

    /// <summary>
    /// Adds a configuration action to the pending queue.
    /// </summary>
    /// <typeparam name="TOption">The option type being configured.</typeparam>
    /// <param name="order">The execution order.</param>
    /// <param name="optionAction">The configuration delegate.</param>
    /// <param name="guideFrom">The source module for this configuration. `null` means direct developer configuration.</param>
    /// <param name="key">The primary configuration method key.</param>
    /// <param name="secondKey">The optional secondary key.</param>
    /// <param name="duplicateBehavior">How duplicate option configuration requests with the same execution identity should be handled.</param>
    public void AddConfigureAction<TOption>(
        int order,
        Action<TOption> optionAction,
        ModuleKey? guideFrom,
        string? secondKey,
        string key,
        ModuleConfigurationDuplicateBehavior duplicateBehavior) where TOption : class, IModuleOptionsBase, new()
    {
        RegisterRequests.Add(
            new ModuleConfigurationRequest($"{key}{secondKey?.BeAfter("_")}")
            {
                ConfigureContext = context =>
                {
                    context.Services!.Configure(optionAction);
                },
                RequestMethod = ModulePhase.ConfigureServices,
                Order = guideFrom != null ? order - 1 : order, // Cascaded module option configuration always runs just before the user-specified order.
                RequestFrom = guideFrom,
                Slot = ModuleConfigurationRequestSlot.Option,
                DuplicateBehavior = duplicateBehavior,
                SourceDesc = $"ConfigOption<{typeof(TOption).Name}>"
            });

        var type = typeof(TOption);
        if (!PendingConfigActions.TryGetValue(type, out var actions))
        {
            actions = new SortedList<int, Action<object>>(new DuplicateKeyComparer<int>());
            PendingConfigActions[type] = actions;
        }
        actions.Add(order, p =>
        {
            optionAction.Invoke((TOption) p);
        });
    }

    /// <summary>
    /// Checks whether all required configuration method keys have been provided.
    /// </summary>
    /// <returns>The missing required method keys, or an empty list if everything is configured.</returns>
    public List<string> GetMissingRequiredConfigMethodKeys()
    {
        if (RequiredConfigMethodKeys.Count == 0)
            return [];

        var configuredKeys = RegisterRequests
            .Select(r => r.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);
        return RequiredConfigMethodKeys
            .Where(requiredKey => !configuredKeys.Any(configuredKey => MatchesRequiredConfigKey(requiredKey, configuredKey)))
            .ToList();
    }

    private static bool MatchesRequiredConfigKey(string requiredKey, string configuredKey)
    {
        return configuredKey.Equals(requiredKey, StringComparison.Ordinal)
            || configuredKey.StartsWith($"{requiredKey}_", StringComparison.Ordinal);
    }

    /// <summary>
    /// Filters register requests to execute only unique configurations.
    /// Duplicate requests use the request's execution key and duplicate behavior.
    /// </summary>
    /// <param name="requests">All register requests to deduplicate</param>
    /// <returns>Deduplicated requests using the existing last-in registration precedence.</returns>
    public IEnumerable<ModuleConfigurationRequest> DeduplicateRequests(
        IEnumerable<ModuleConfigurationRequest> requests)
    {
        var logger = application.CreateLogger<ModuleRegistrationState>();
        var requestList = requests.ToList();
        var lastRequestIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < requestList.Count; index++)
        {
            lastRequestIndexByKey[requestList[index].ExecutionKey] = index;
        }

        for (var index = 0; index < requestList.Count; index++)
        {
            var request = requestList[index];
            if (lastRequestIndexByKey[request.ExecutionKey] == index)
            {
                yield return request;
            }
            else if (request.DuplicateBehavior == ModuleConfigurationDuplicateBehavior.Warn)
            {
                logger.LogWarning(
                    "Skipping duplicate configuration: {Key} (Slot: {Slot}, OwningModule: {OwningModule}, RequestFrom: {RequestFrom}, RequestMethod: {Method}, Order: {Order}, SourceDesc: {SourceDesc})",
                    request.Key, request.Slot, ModuleType.Name, request.RequestFrom?.ToString() ?? "N/A", request.RequestMethod, request.Order, request.SourceDesc ?? "N/A");
            }
        }
    }


    public override string ToString()
    {
        return $"{ModulePhase} - {ModuleType.Name}";
    }
}

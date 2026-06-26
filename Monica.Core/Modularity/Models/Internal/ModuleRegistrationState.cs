using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Stores module registration requests and configuration data.
/// </summary>
public class ModuleRegistrationState(Type moduleType)
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
        ModuleInitializationProfiler.StartModulePhase(ModuleType, phase);
        SetModulePhase(phase);
    }

    public void EndModulePhase(ModulePhase phase)
    {
        ModuleInitializationProfiler.StopModulePhase(ModuleType, phase);
    }

    /// <summary>
    /// Creates a module option instance based on the configuration actions known so far.
    /// Intended only for exceptional cases during early registration.
    /// </summary>
    /// <returns>The current module option instance.</returns>
    public object CreateCurrentModuleOption()
    {
        var currentModuleOption = Activator.CreateInstance(ModuleOptionType)!;
        
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
        foreach (var configType in PendingConfigActions.Keys)
        {
            // Create an instance for the configuration type.
            var configInstance = Activator.CreateInstance(configType);
            
            if (configInstance == null)
                continue;
            
            // Retrieve all actions for the type, already sorted by priority.
            var sortedActions = PendingConfigActions[configType];
            
            // Apply each configuration action in order.
            foreach (var action in sortedActions.Values)
            {
                action.Invoke(configInstance);
            }
            
            // Persist the finalized configuration instance.
            FinalConfigures[configType] = configInstance;
        }

        if(!FinalConfigures.ContainsKey(ModuleOptionType))
        {
            FinalConfigures[ModuleOptionType] = Activator.CreateInstance(ModuleOptionType)!;
        }

        if (Activator.CreateInstance(ModuleType, ModuleOption) is ModuleBase instance)
        {
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

    /// <summary>
    /// Binds the primary module option type.
    /// </summary>
    /// <typeparam name="TOption">The module option type.</typeparam>
    public void BindModuleOption<TOption>() where TOption : class, IModuleOptions, new()
    {
        ModuleOptionType = typeof(TOption);
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
        var logger = LogManager.For<ModuleRegistrationState>();
        var seenKeys = new HashSet<string>();

        foreach (var request in requests.Reverse())
        {
            if (seenKeys.Add(request.ExecutionKey))
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

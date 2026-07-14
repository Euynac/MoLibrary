using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Abstractions;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Represents a module snapshot, including the module instance, registration info, and state.
/// </summary>
/// <param name="application">The owning Monica application.</param>
/// <param name="moduleInstance">The module instance.</param>
/// <param name="registerInfo">The registration information.</param>
public class ModuleRuntimeSnapshot(
    MonicaApplication application,
    ModuleBase moduleInstance,
    ModuleRegistrationState registerInfo)
{
    /// <summary>
    /// The module instance.
    /// </summary>
    public IModule ModuleInstance { get; set; } = moduleInstance;

    /// <summary>
    /// The module registration information.
    /// </summary>
    public ModuleRegistrationState RegisterInfo { get; set; } = registerInfo;

    /// <summary>
    /// The module type.
    /// </summary>
    public Type ModuleType { get; set; } = moduleInstance.GetType();

    /// <summary>
    /// Gets whether the module participates in the ASP.NET Core lifecycle.
    /// </summary>
    public bool IsWebModule => ModuleInstance is IWebModule;

    /// <summary>
    /// Gets whether the module was accepted in a generic host by downgrading its web behavior.
    /// </summary>
    public bool IsDowngradedFromWebModule => RegisterInfo.IsDowngradedFromWebModule;

    /// <summary>
    /// Gets the <see cref="ModuleKey"/> for the module.
    /// </summary>
    public ModuleKey ModuleKey => application.Dependencies.ResolveModuleKey(ModuleType);

    /// <summary>
    /// Gets the total initialization duration for the module, in milliseconds.
    /// </summary>
    public long TotalInitializationDurationMs =>
        application.Profiling.GetModuleTotalDuration(ModuleType);


    public override string ToString()
    {
        return $"[{ModuleKey}] {RegisterInfo}";
    }

    /// <summary>
    /// Gets the keyed option for this module from DI container.
    /// Uses ModuleOptionType from RegisterInfo and IOptionsSnapshot for retrieval.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="serviceKey">The keyed service key, or null for default option</param>
    /// <returns>Tuple of option type and option instance</returns>
    public (Type OptionType, object? OptionInstance) GetKeyedOption(
        IServiceProvider serviceProvider,
        string? serviceKey)
    {
        var optionType = RegisterInfo.ModuleOptionType;

        if (serviceKey == null)
        {
            // For non-keyed (default) option, use IOptions<T>
            var optionsType = typeof(IOptions<>).MakeGenericType(optionType);
            var options = serviceProvider.GetService(optionsType);
            var value = options?.GetType().GetProperty("Value")?.GetValue(options);
            return (optionType, value);
        }

        // For keyed option, use IOptionsSnapshot<T>.Get(key)
        var snapshotType = typeof(IOptionsSnapshot<>).MakeGenericType(optionType);
        var snapshot = serviceProvider.GetService(snapshotType);
        if (snapshot == null) return (optionType, null);

        var getMethod = snapshotType.GetMethod("Get");
        var instance = getMethod?.Invoke(snapshot, [serviceKey]);
        return (optionType, instance);
    }
}

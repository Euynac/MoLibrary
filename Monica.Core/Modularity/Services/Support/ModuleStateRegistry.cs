using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.State;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Manages module state including enabling and disabling modules.
/// </summary>
internal sealed class ModuleStateRegistry(MonicaApplication application)
{
    private readonly ModuleState _state = new();

    public ILogger Logger => application.CreateLogger(typeof(ModuleStateRegistry));

    /// <summary>
    /// Clears module enablement state for this host.
    /// </summary>
    internal void Clear()
    {
        _state.Clear();
    }

    /// <summary>
    /// Gets the list of disabled module types
    /// </summary>
    /// <returns>A list of disabled module types</returns>
    internal List<Type> GetDisabledModuleTypes()
    {
        return [.. _state.DisabledModuleTypes];
    }

    public bool DisableModule(ModuleRegistrationState moduleInfo)
    {
        moduleInfo.SetModulePhase(ModulePhase.Disabled);
        return DisableModule(moduleInfo.ModuleType);
    }


    /// <summary>
    /// Disables a module due to an exception or configuration
    /// </summary>
    /// <param name="moduleType">The type of module to disable</param>
    /// <returns>True if the module was successfully disabled, false if it was already disabled</returns>
    private bool DisableModule(Type moduleType)
    {
        if (!_state.DisabledModuleTypes.Add(moduleType)) return false;
        CascadeDisableModulesThatDependOn(moduleType);
        return true;

    }

    /// <summary>
    /// Checks if a module is disabled
    /// </summary>
    /// <param name="moduleType">The type of module to check</param>
    /// <returns>True if the module is disabled, false otherwise</returns>
    internal bool IsModuleDisabled(Type moduleType)
    {
        // Check if the module is in the disabled list
        if (_state.DisabledModuleTypes.Contains(moduleType))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Cascade disables modules that depend on the specified module
    /// </summary>
    /// <param name="moduleType">The module type that other modules might depend on</param>
    internal void CascadeDisableModulesThatDependOn(Type moduleType)
    {
        // Find the module key for the disabled module
        var disabledModuleKey = application.Dependencies.ResolveModuleKey(moduleType);

        // Get all modules that depend on this module from the dependency map
        var dependentModuleKeys = new HashSet<ModuleKey>();
        foreach (var entry in application.Dependencies.DependenciesByModule)
        {
            if (entry.Value.Contains(disabledModuleKey))
            {
                dependentModuleKeys.Add(entry.Key);
            }
        }

        // Disable all dependent modules
        foreach (var dependentModuleKey in dependentModuleKeys)
        {
            // Skip if not registered in the key-to-type map
            if (!application.Dependencies.ModuleTypesByKey.TryGetValue(dependentModuleKey, out var dependentModuleType))
                continue;

            if (DisableModule(dependentModuleType))
            {
                Logger.LogWarning(
                    "Module {ModuleName} was disabled because it depends on disabled module {DisabledModuleName}",
                    dependentModuleType.Name,
                    moduleType.Name);

                // Recursively cascade disable
                CascadeDisableModulesThatDependOn(dependentModuleType);
            }
        }
    }
    /// <summary>
    /// Initializes the module system by checking for disabled modules and cascading the disable status to dependent modules.
    /// </summary>
    internal void Init()
    {
        // Check each registered module to see if it's disabled
        foreach (var (moduleType, info) in application.Modules.Registrations)
        {
            if (!info.ModuleOption.IsDisabled) continue;
            DisableModule(info);
            Logger.LogWarning("Module {ModuleName} is disabled by configuration", moduleType.Name);
        }
    }
}

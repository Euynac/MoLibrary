using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Manages module state including enabling and disabling modules.
/// </summary>
public static class ModuleStateRegistry
{
    public static ILogger Logger { get; set; } = LogManager.For(typeof(ModuleStateRegistry));

    /// <summary>
    /// List of disabled module types
    /// </summary>
    private static HashSet<Type> DisabledModuleTypes { get; } = new();

    /// <summary>
    /// Gets the list of disabled module types
    /// </summary>
    /// <returns>A list of disabled module types</returns>
    internal static List<Type> GetDisabledModuleTypes()
    {
        return [.. DisabledModuleTypes];
    }

    public static bool DisableModule(ModuleRegistrationState moduleInfo)
    {
        moduleInfo.SetModulePhase(ModulePhase.Disabled);
        return DisableModule(moduleInfo.ModuleType);
    }


    /// <summary>
    /// Disables a module due to an exception or configuration
    /// </summary>
    /// <param name="moduleType">The type of module to disable</param>
    /// <returns>True if the module was successfully disabled, false if it was already disabled</returns>
    private static bool DisableModule(Type moduleType)
    {
        if (!DisabledModuleTypes.Add(moduleType)) return false;
        CascadeDisableModulesThatDependOn(moduleType);
        return true;

    }

    /// <summary>
    /// Checks if a module is disabled
    /// </summary>
    /// <param name="moduleType">The type of module to check</param>
    /// <returns>True if the module is disabled, false otherwise</returns>
    internal static bool IsModuleDisabled(Type moduleType)
    {
        // Check if the module is in the disabled list
        if (DisabledModuleTypes.Contains(moduleType))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Cascade disables modules that depend on the specified module
    /// </summary>
    /// <param name="moduleType">The module type that other modules might depend on</param>
    internal static void CascadeDisableModulesThatDependOn(Type moduleType)
    {
        // Find the module key for the disabled module
        var disabledModuleKey = ModuleDependencyAnalyzer.ResolveModuleKey(moduleType);

        // Get all modules that depend on this module from the dependency map
        var dependentModuleKeys = new HashSet<ModuleKey>();
        foreach (var entry in ModuleDependencyAnalyzer.ModuleDependencyMap)
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
            if (!ModuleDependencyAnalyzer.ModuleKeyToTypeDict.TryGetValue(dependentModuleKey, out var dependentModuleType))
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
    internal static void Init()
    {
        // Check each registered module to see if it's disabled
        foreach (var (moduleType, info) in ModuleRegistry.ModuleRegisterContextDict)
        {
            if (!info.ModuleOption.IsDisabled) continue;
            DisableModule(info);
            Logger.LogWarning("Module {ModuleName} is disabled by configuration", moduleType.Name);
        }
    }
}

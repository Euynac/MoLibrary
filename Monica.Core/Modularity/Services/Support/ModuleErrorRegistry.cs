using System.Text;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Utility class for handling module-related errors.
/// </summary>
public static class ModuleErrorRegistry
{

    /// <summary>
    /// Module registration errors.
    /// </summary>
    public static List<ModuleRegistrationError> ModuleRegisterErrors { get; } = [];
    /// <summary>
    /// Validates module configuration requirements and throws an exception if requirements are not met.
    /// </summary>
    /// <param name="moduleRegisterContextDict">Dictionary of module registration contexts.</param>
    /// <exception cref="ModuleRegistrationException">Thrown when module registration requirements are not met.</exception>
    public static void ValidateModuleRequirements(
        Dictionary<Type, ModuleRegistrationState> moduleRegisterContextDict)
    {
        // Check if modules meet necessary configuration requirements
        foreach (var (moduleType, info) in moduleRegisterContextDict)
        {
            // Check if there are required configuration method keys
            if (info.RequiredConfigMethodKeys.Count == 0) continue;
            
            // Check if each required configuration method key is configured
            var missingKeys = info.GetMissingRequiredConfigMethodKeys();
           
            if (missingKeys.Count > 0)
            {
                var error = new ModuleRegistrationError
                {
                    ModuleType = moduleType,
                    ErrorMessage = "Missing required configuration",
                    MissingConfigKeys = missingKeys,
                    ErrorType = ModuleRegistrationErrorType.MissingRequiredConfig
                };
                if(!CheckDisableModuleIfHasException(moduleType, error)) ModuleRegisterErrors.Add(error);
            }

        }

        //// Check for missing dependencies
        //ValidateDependencies();


        RaiseModuleErrors();
    }
    //// 1.1 Check for circular dependencies in the dependency graph
    //if (ModuleDependencyAnalyzer.HasCircularDependencies())
    //{
    //    var error = new ModuleRegistrationError
    //    {
    //        ErrorMessage = "Circular dependency detected in module dependencies. Please check the dependency graph.",
    //        ErrorType = ModuleRegistrationErrorType.CircularDependency
    //    };
    //    ModuleRegisterErrors.Add(error);

    //    // Log the dependency graph for debugging
    //    var graph = ModuleDependencyAnalyzer.CalculateCompleteModuleDependencyGraph();

    //   Logger.LogError("Module dependency graph contains circular dependencies:\n{Graph}", graph.ToString());

    //    // Continue with registration but warn about potential issues
    //}
    //else
    //{
    //    var modulesInOrder = ModuleDependencyAnalyzer.GetModulesInDependencyOrder();

    //    var graph = ModuleDependencyAnalyzer.CalculateCompleteModuleDependencyGraph();
    //    Logger.LogInformation("Modules will be initialized in the following dependency order: {Modules}\n{Graph}",
    //        string.Join(", ", modulesInOrder), graph);
    //}

    /// <summary>
    /// Validates dependencies between modules using the ModuleDependencyAnalyzer.
    /// </summary>
    private static void ValidateDependencies()
    {
        // Check for circular dependencies
        if (ModuleDependencyAnalyzer.HasCircularDependencies())
        {
            // Find modules involved in cycles
            foreach (var moduleKey in ModuleDependencyAnalyzer.ModuleKeyToTypeDict.Keys)
            {
                var dependencyInfo = ModuleDependencyAnalyzer.GetModuleDependencyInfo(moduleKey);

                if (dependencyInfo.IsPartOfCycle)
                {
                    // Get module type from key using the direct mapping
                    if (!ModuleDependencyAnalyzer.ModuleKeyToTypeDict.TryGetValue(moduleKey, out var moduleType) || moduleType == null)
                        continue;

                    ModuleRegisterErrors.Add(new ModuleRegistrationError
                    {
                        ModuleType = moduleType,
                        ErrorMessage = $"Module is part of a circular dependency chain: {string.Join(" → ", dependencyInfo.CyclePath)} → {moduleKey}",
                        ErrorType = ModuleRegistrationErrorType.CircularDependency,
                        DependencyInfo = dependencyInfo
                    });
                }
            }
        }
    }
    
    /// <summary>
    /// Records an error that occurred during module configuration.
    /// </summary>
    /// <param name="moduleType">The type of the module where the error occurred.</param>
    /// <param name="exception">The error.</param>
    /// <param name="phase">The phase where the error occurred.</param>
    /// <param name="errorType">The type of error.</param>
    public static void RecordModuleError(
        Type moduleType, 
        Exception exception, 
        ModulePhase phase, 
        ModuleRegistrationErrorType errorType)
    {
        var error = new ModuleRegistrationError 
        {
            ModuleType = moduleType,
            ErrorMessage = $"Error in {phase}: {exception.GetMessageRecursively()}",
            ErrorType = errorType,
            Phase = phase,
            StackTrace = exception.StackTrace
        };
        ModuleRegisterErrors.Add(error);
        
        if(phase > ModulePhase.InitFinalConfigures)
        {
            // Immediately check if the module should be disabled due to exception
            CheckDisableModuleIfHasException(moduleType, error);
        }

    }
    
    /// <summary>
    /// Records an error that occurred during a module request.
    /// </summary>
    /// <param name="moduleType">The type of the module where the error occurred.</param>
    /// <param name="request">The request that caused the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void RecordRequestError(
        Type moduleType, 
        ModuleConfigurationRequest request, 
        Exception exception)
    {
        var error = new ModuleRegistrationError
        {
            ModuleType = moduleType,
            ErrorMessage = $"Error in request {request.Key} (from {request.RequestFrom}): {exception.GetMessageRecursively()}",
            ErrorType = ModuleRegistrationErrorType.ConfigurationError,
            GuideFrom = request.RequestFrom,
            Phase = request.RequestMethod,
            StackTrace = exception.StackTrace
        };
        ModuleRegisterErrors.Add(error);
        
        // Immediately check if the module should be disabled due to exception
        CheckDisableModuleIfHasException(moduleType, error);
    }
    
    /// <summary>
    /// Checks if a module has DisableModuleIfHasException set and disables it if needed
    /// </summary>
    /// <param name="moduleType">The module type to check</param>
    /// <param name="error">The error that occurred</param>
    /// <returns>If it should disable module, return true.</returns>
    internal static bool CheckDisableModuleIfHasException(Type moduleType, ModuleRegistrationError error)
    {
        // Try to find the module option through ModuleRegistry
        if (ModuleRegistry.TryGetModuleRequestInfo(moduleType, out var requestInfo))
        {
            // Check if the module has DisableModuleIfHasException set
            var moduleOption = requestInfo.ModuleOption;

            if (moduleOption.DisableModuleIfHasException ?? Mo.Options.DisableModuleIfHasException)
            {
                // Disable the module
                if (ModuleStateRegistry.DisableModule(requestInfo))
                {
                    // Log the error but don't throw an exception for this module
                    ModuleRegistry.Logger.LogWarning(
                        "Module {ModuleName} was disabled due to an error: {ErrorMessage}",
                        moduleType.Name,
                        error);
                }

                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Builds a detailed error message from a list of module registration errors.
    /// </summary>
    /// <param name="errors">List of module registration errors.</param>
    /// <returns>A formatted error message.</returns>
    public static string BuildErrorMessage(List<ModuleRegistrationError> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Module registration errors:");
        
        // Group errors by module
        var groupedErrors = errors.GroupBy(e => e.ModuleType);
        
        foreach (var group in groupedErrors)
        {
            sb.AppendLine($"Module {group.Key?.Name ?? "Unknown"}:");
            
            foreach (var error in group)
            {
                sb.Append(error);
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
    
   
    /// <summary>
    /// Raises an exception if there are any module registration errors.
    /// </summary>
    public static void RaiseModuleErrors()
    {
        if (ModuleRegisterErrors.Count == 0)
        {
            return;
        }

        // Filter out errors for modules that have already been disabled
        var errorsToThrow = ModuleRegisterErrors.Where(e => !ModuleStateRegistry.IsModuleDisabled(e.ModuleType)).ToList();
        
        //// Log summary of disabled modules
        //var disabledModules = ModuleStateRegistry.GetDisabledModuleTypes();
        //if (disabledModules.Count > 0)
        //{
        //    ModuleRegistry.Logger.LogWarning(
        //        "The following modules were disabled due to exceptions: {DisabledModules}",
        //        string.Join(", ", disabledModules.Select(m => m.Name)));
        //}

        // Throw exception if there are any remaining errors
        if (errorsToThrow.Count > 0)
        {
            throw new ModuleRegistrationException(BuildErrorMessage(errorsToThrow));
        }
    }

} 

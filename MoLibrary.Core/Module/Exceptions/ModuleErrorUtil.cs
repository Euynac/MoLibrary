using System.Text;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.ModuleAnalyser;
using MoLibrary.Tool.MoResponse;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Utility class for handling module-related errors.
/// </summary>
public static class ModuleErrorUtil
{
    /// <summary>
    /// Validates module configuration requirements and throws an exception if requirements are not met.
    /// </summary>
    /// <param name="moduleRegisterContextDict">Dictionary of module registration contexts.</param>
    /// <param name="moduleRegisterErrors">List to store any registration errors.</param>
    /// <exception cref="ModuleRegisterException">Thrown when module registration requirements are not met.</exception>
    public static void ValidateModuleRequirements(
        Dictionary<Type, ModuleRequestInfo> moduleRegisterContextDict,
        List<ModuleRegisterError> moduleRegisterErrors)
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
                moduleRegisterErrors.Add(new ModuleRegisterError
                {
                    ModuleType = moduleType,
                    ErrorMessage = "Missing required configuration",
                    MissingConfigKeys = missingKeys,
                    ErrorType = ModuleRegisterErrorType.MissingRequiredConfig
                });
            }
        }
        
        // Check for missing dependencies
        ValidateDependencies(moduleRegisterErrors);
        
        // If there are errors, throw an exception
        if (moduleRegisterErrors.Count > 0)
        {
            throw new ModuleRegisterException(BuildErrorMessage(moduleRegisterErrors));
        }
    }
    
    /// <summary>
    /// Validates dependencies between modules using the ModuleAnalyser.
    /// </summary>
    /// <param name="moduleRegisterErrors">List to store any dependency-related errors.</param>
    private static void ValidateDependencies(List<ModuleRegisterError> moduleRegisterErrors)
    {
        // Check for circular dependencies
        if (MoModuleAnalyser.HasCircularDependencies())
        {
            // Find modules involved in cycles
            foreach (var module in Enum.GetValues(typeof(EMoModules)).Cast<EMoModules>())
            {
                var dependencyInfo = MoModuleAnalyser.GetModuleDependencyInfo(module);
                
                if (dependencyInfo.IsPartOfCycle)
                {
                    // Get module type from enum using the direct mapping
                    if (!MoModuleAnalyser.ModuleEnumToTypeDict.TryGetValue(module, out var moduleType) || moduleType == null)
                        continue;
                    
                    moduleRegisterErrors.Add(new ModuleRegisterError
                    {
                        ModuleType = moduleType,
                        ErrorMessage = $"Module is part of a circular dependency chain: {string.Join(" → ", dependencyInfo.CyclePath)} → {module}",
                        ErrorType = ModuleRegisterErrorType.CircularDependency,
                        DependencyInfo = dependencyInfo
                    });
                }
            }
        }
    }
    
    /// <summary>
    /// Records an error that occurred during module configuration.
    /// </summary>
    /// <param name="moduleRegisterErrors">List to store registration errors.</param>
    /// <param name="moduleType">The type of the module where the error occurred.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="phase">The phase where the error occurred.</param>
    /// <param name="errorType">The type of error.</param>
    public static void RecordModuleError(
        List<ModuleRegisterError> moduleRegisterErrors,
        Type moduleType, 
        string errorMessage, 
        EMoModuleConfigMethods phase, 
        ModuleRegisterErrorType errorType)
    {
        var error = new ModuleRegisterError
        {
            ModuleType = moduleType,
            ErrorMessage = $"Error in {phase}: {errorMessage}",
            ErrorType = errorType,
            Phase = phase
        };
        moduleRegisterErrors.Add(error);
        
        if(phase > EMoModuleConfigMethods.ClaimDependencies)
        {
            // Immediately check if the module should be disabled due to exception
            CheckDisableModuleIfHasException(moduleType, error);
        }

    }
    
    /// <summary>
    /// Records an error that occurred during a module request.
    /// </summary>
    /// <param name="moduleRegisterErrors">List to store registration errors.</param>
    /// <param name="moduleType">The type of the module where the error occurred.</param>
    /// <param name="request">The request that caused the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void RecordRequestError(
        List<ModuleRegisterError> moduleRegisterErrors,
        Type moduleType, 
        ModuleRegisterRequest request, 
        Exception exception)
    {
        var error = new ModuleRegisterError
        {
            ModuleType = moduleType,
            ErrorMessage = $"Error in request {request.Key} (from {request.RequestFrom}): {exception.Message}",
            ErrorType = ModuleRegisterErrorType.ConfigurationError,
            GuideFrom = request.RequestFrom,
            Phase = request.RequestMethod
        };
        moduleRegisterErrors.Add(error);
        
        // Immediately check if the module should be disabled due to exception
        CheckDisableModuleIfHasException(moduleType, error);
    }
    
    /// <summary>
    /// Checks if a module has DisableModuleIfHasException set and disables it if needed
    /// </summary>
    /// <param name="moduleType">The module type to check</param>
    /// <param name="error">The error that occurred</param>
    private static void CheckDisableModuleIfHasException(Type moduleType, ModuleRegisterError error)
    {
        // Try to find the module option through MoModuleRegisterCentre
        if (MoModuleRegisterCentre.ModuleRegisterContextDict.TryGetValue(moduleType, out var requestInfo))
        {
            // Check if the module has DisableModuleIfHasException set
            var moduleOption = requestInfo.ModuleOption;

            if (moduleOption.DisableModuleIfHasException)
            {
                // Disable the module
                if (MoModuleRegisterCentre.DisableModule(moduleType))
                {
                    // Log the error but don't throw an exception for this module
                    MoModuleRegisterCentre.Logger.LogWarning(
                        "Module {ModuleName} was disabled due to an exception: {ErrorMessage}",
                        moduleType.Name,
                        error.ErrorMessage);
                    
                    // Check for cascade disabling of dependent modules
                    MoModuleRegisterCentre.CascadeDisableModulesThatDependOn(moduleType);
                }
            }
        }
    }
    
    /// <summary>
    /// Builds a detailed error message from a list of module registration errors.
    /// </summary>
    /// <param name="errors">List of module registration errors.</param>
    /// <returns>A formatted error message.</returns>
    public static string BuildErrorMessage(List<ModuleRegisterError> errors)
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
                sb.AppendLine($"  - Error Type: {error.ErrorType}");
                sb.AppendLine($"  - Details: {error.ErrorMessage}");
                
                if (error.GuideFrom.HasValue)
                {
                    sb.AppendLine($"  - Source: {error.GuideFrom}");
                }
                
                if (error.Phase.HasValue)
                {
                    sb.AppendLine($"  - Phase: {error.Phase}");
                }
                
                if (error.MissingConfigKeys.Count > 0)
                {
                    sb.AppendLine("  - Missing configuration methods:");
                    
                    foreach (var key in error.MissingConfigKeys)
                    {
                        sb.AppendLine($"    * {key}");
                    }
                }
                
                if (error.DependencyInfo != null)
                {
                    sb.AppendLine("  - Dependency Information:");
                    sb.AppendLine($"    {error.DependencyInfo}");
                }
                
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
    
   
    /// <summary>
    /// Raises an exception if there are any module registration errors.
    /// </summary>
    /// <param name="errors">List of module registration errors.</param>
    public static void RaiseModuleErrors(List<ModuleRegisterError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        // Filter out errors for modules that have already been disabled
        var errorsToThrow = errors.Where(e => !MoModuleRegisterCentre.IsModuleDisabled(e.ModuleType)).ToList();
        
        // Log summary of disabled modules
        var disabledModules = MoModuleRegisterCentre.GetDisabledModuleTypes();
        if (disabledModules.Count > 0)
        {
            MoModuleRegisterCentre.Logger.LogWarning(
                "The following modules were disabled due to exceptions: {DisabledModules}",
                string.Join(", ", disabledModules.Select(m => m.Name)));
        }

        // Throw exception if there are any remaining errors
        if (errorsToThrow.Count > 0)
        {
            throw new ModuleRegisterException(BuildErrorMessage(errorsToThrow));
        }
    }

} 
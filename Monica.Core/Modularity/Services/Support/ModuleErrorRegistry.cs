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
internal sealed class ModuleErrorRegistry(MonicaApplication application)
{
    /// <summary>
    /// Validates module configuration requirements and throws an exception if requirements are not met.
    /// </summary>
    /// <param name="moduleRegisterContextDict">Dictionary of module registration contexts.</param>
    /// <exception cref="ModuleRegistrationException">Thrown when module registration requirements are not met.</exception>
    public void ValidateModuleRequirements(
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
                if (!CheckDisableModuleIfHasException(moduleType, error))
                {
                    application.Modules.AddRegistrationError(error);
                }
            }

        }

        RaiseModuleErrors();
    }

    /// <summary>
    /// Validates the complete dependency graph before any host services are mutated.
    /// </summary>
    internal void ValidateDependencyGraph()
    {
        if (!application.Dependencies.HasCircularDependencies())
        {
            return;
        }

        var moduleTypesByKey = application.Dependencies.ModuleTypesByKey;
        foreach (var moduleKey in moduleTypesByKey.Keys)
        {
            var dependencyInfo = application.Dependencies.GetModuleDependencyInfo(moduleKey);
            if (!dependencyInfo.IsPartOfCycle ||
                !moduleTypesByKey.TryGetValue(moduleKey, out var moduleType))
            {
                continue;
            }

            application.Modules.AddRegistrationError(new ModuleRegistrationError
            {
                ModuleType = moduleType,
                ErrorMessage = $"Module is part of a circular dependency chain: {string.Join(" → ", dependencyInfo.CyclePath)} → {moduleKey}",
                ErrorType = ModuleRegistrationErrorType.CircularDependency,
                DependencyInfo = dependencyInfo
            });
        }

        RaiseModuleErrors();
    }
    
    /// <summary>
    /// Records an error that occurred during module configuration.
    /// </summary>
    /// <param name="moduleType">The type of the module where the error occurred.</param>
    /// <param name="exception">The error.</param>
    /// <param name="phase">The phase where the error occurred.</param>
    /// <param name="errorType">The type of error.</param>
    public void RecordModuleError(
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
        application.Modules.AddRegistrationError(error);
        
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
    public void RecordRequestError(
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
        application.Modules.AddRegistrationError(error);
        
        // Immediately check if the module should be disabled due to exception
        CheckDisableModuleIfHasException(moduleType, error);
    }

    /// <summary>
    /// Records an error indicating that the current host cannot satisfy a module's ASP.NET Core requirements.
    /// </summary>
    /// <param name="moduleType">The incompatible module type.</param>
    /// <param name="message">The compatibility error message.</param>
    public void RecordHostCompatibilityError(Type moduleType, string message)
    {
        application.Modules.AddRegistrationError(new ModuleRegistrationError
        {
            ModuleType = moduleType,
            ErrorMessage = message,
            ErrorType = ModuleRegistrationErrorType.HostCompatibility
        });
    }
    
    /// <summary>
    /// Checks if a module has DisableModuleIfHasException set and disables it if needed
    /// </summary>
    /// <param name="moduleType">The module type to check</param>
    /// <param name="error">The error that occurred</param>
    /// <returns>If it should disable module, return true.</returns>
    internal bool CheckDisableModuleIfHasException(Type moduleType, ModuleRegistrationError error)
    {
        // Try to find the module option through ModuleRegistry
        if (application.Modules.TryGetModuleRequestInfo(moduleType, out var requestInfo))
        {
            // Check if the module has DisableModuleIfHasException set
            var moduleOption = requestInfo.ModuleOption;

            if (moduleOption.DisableModuleIfHasException ?? application.ModuleSystem.DisableOnRegistrationError)
            {
                // Disable the module
                if (application.ModuleStates.DisableModule(requestInfo))
                {
                    // Log the error but don't throw an exception for this module
                    application.Modules.Logger.LogWarning(
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
    public string BuildErrorMessage(List<ModuleRegistrationError> errors)
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
    public void RaiseModuleErrors()
    {
        if (application.Modules.RegistrationErrors.Count == 0)
        {
            return;
        }

        // Filter out errors for modules that have already been disabled
        var errorsToThrow = application.Modules.RegistrationErrors
            .Where(e => !application.ModuleStates.IsModuleDisabled(e.ModuleType))
            .ToList();
        
        //// Log summary of disabled modules
        //var disabledModules = application.ModuleStates.GetDisabledModuleTypes();
        //if (disabledModules.Count > 0)
        //{
        //    application.Modules.Logger.LogWarning(
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

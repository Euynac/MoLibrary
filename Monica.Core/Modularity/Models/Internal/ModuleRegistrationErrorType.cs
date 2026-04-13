namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Defines the types of errors that can occur during module registration.
/// </summary>
public enum ModuleRegistrationErrorType
{
    /// <summary>
    /// A general error with no specific type.
    /// </summary>
    General = 0,
    
    /// <summary>
    /// Error indicating missing required configuration for a module.
    /// </summary>
    MissingRequiredConfig = 1,
    
    /// <summary>
    /// Error indicating a circular dependency was detected in the module dependency graph.
    /// </summary>
    CircularDependency = 2,
    
    /// <summary>
    /// Error indicating a required module dependency is missing.
    /// </summary>
    MissingDependency = 3,
    
    /// <summary>
    /// Error during module initialization.
    /// </summary>
    InitializationError = 4,
    
    /// <summary>
    /// Error during module configuration.
    /// </summary>
    ConfigurationError = 5,

    /// <summary>
    /// Error indicating that the current host cannot satisfy a module's ASP.NET Core requirements.
    /// </summary>
    HostCompatibility = 6
} 

using Monica.Core.Module.Features;

namespace Monica.Core.Module.Exceptions;

/// <summary>
/// Base exception class for all module-related exceptions in Monica.
/// </summary>
public class ModuleException(string message) : Exception(message)
{
    /// <summary>
    /// Module type where the exception occurred.
    /// </summary>
    public Type? ModuleType { get; set; }
    
    /// <summary>
    /// Module dependencies information when the exception occurred.
    /// </summary>
    public ModuleDependencyInfo? DependencyInfo { get; set; }
} 
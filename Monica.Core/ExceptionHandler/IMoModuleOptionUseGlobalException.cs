namespace Monica.Core.ExceptionHandler;

public interface IMoModuleOptionUseGlobalException
{
    /// <summary>
    /// Gets or sets whether the global exception handler is disabled for the module.
    /// </summary>
    bool DisableGlobalExceptionHandler { get; set; }
}

namespace Monica.DataChannel.Interfaces;

/// <summary>
/// Defines the entry point for configuring and initializing data channel pipelines.
/// Implementations are responsible for building and registering all required pipelines.
/// </summary>
public interface ISetupPipeline
{
    /// <summary>
    /// Configures the pipelines.
    /// Create, configure, and register all data pipelines in this method.
    /// The framework calls it automatically during application startup.
    /// </summary>
    void Setup();
}

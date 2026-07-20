namespace Monica.DataChannel.Abstractions;

/// <summary>
/// Defines the entry point for configuring and initializing data channel pipelines.
/// Implementations declare every pipeline required by one application host.
/// </summary>
public interface IDataChannelSetup
{
    /// <summary>
    /// Declares the pipelines owned by the current application host.
    /// </summary>
    /// <param name="channels">
    /// The host-owned registrar. It remains valid only until the framework materializes the registered pipelines.
    /// </param>
    void Setup(IDataChannelRegistrar channels);
}

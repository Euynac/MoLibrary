using Monica.Core.Modularity.Abstractions;
using Monica.Modules;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Describes one configuration store that can be projected into both runtime module composition and
/// pre-container startup reading.
/// </summary>
/// <remarks>
/// Implementations must configure equivalent logical stores for both phases. The startup store must be independently
/// owned from the runtime store; the snapshot-loading operation disposes it when it supports disposal.
/// Composition topology must remain immutable after plan creation, and both methods must deterministically target the
/// same logical store from captured values that remain stable across both phases.
/// </remarks>
public interface IMonicaConfigurationStoreComposition
{
    /// <summary>
    /// Gets a stable, developer-facing name used in composition diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Applies this store to the runtime Configuration module registration.
    /// </summary>
    /// <param name="module">The host-bound Configuration module registration.</param>
    /// <remarks>This method records runtime registrations only and must not perform store I/O.</remarks>
    void ConfigureRuntime(ModuleRegistration<ModuleConfiguration, ModuleConfigurationOption> module);

    /// <summary>
    /// Creates an isolated effective-value store for startup options loading.
    /// </summary>
    /// <returns>
    /// A new, independently owned store. The caller disposes it when it supports synchronous or asynchronous disposal.
    /// </returns>
    IConfigurationEffectiveValueStore CreateStartupStore();
}

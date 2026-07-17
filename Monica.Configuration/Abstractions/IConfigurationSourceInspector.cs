using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Inspects runtime Microsoft configuration providers and resolves source chains for managed definitions.
/// </summary>
public interface IConfigurationSourceInspector
{
    /// <summary>
    /// Lists runtime configuration sources in provider registration order.
    /// </summary>
    /// <returns>The source descriptors.</returns>
    IReadOnlyList<ConfigurationSourceDescriptor> GetSources();

    /// <summary>
    /// Gets one source descriptor or throws when the source is unknown.
    /// </summary>
    /// <param name="sourceKey">The source key.</param>
    /// <returns>The source descriptor.</returns>
    ConfigurationSourceDescriptor GetRequiredSource(string sourceKey);

    /// <summary>
    /// Gets the source chain for one definition path.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <returns>The source chain.</returns>
    ConfigurationSourceChain GetSourceChain(ConfigurationDefinition definition, LogicalPath logicalPath);

    /// <summary>
    /// Gets source chains for several paths while sharing one runtime-provider snapshot.
    /// </summary>
    /// <param name="definition">The managed definition.</param>
    /// <param name="logicalPaths">The logical paths to inspect, in result order.</param>
    /// <returns>One source chain for each requested path.</returns>
    IReadOnlyList<ConfigurationSourceChain> GetSourceChains(
        ConfigurationDefinition definition,
        IReadOnlyList<LogicalPath> logicalPaths);

    /// <summary>
    /// Gets an opaque revision of one runtime provider's schema-visible contribution to a definition.
    /// Sensitive values participate in the revision but are never returned to the caller.
    /// </summary>
    /// <param name="definition">The managed definition.</param>
    /// <param name="sourceKey">The source key.</param>
    /// <returns>The provider projection revision.</returns>
    string GetRuntimeProjectionRevision(ConfigurationDefinition definition, string sourceKey);

    /// <summary>
    /// Gets source contribution counts for a definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The contribution rows.</returns>
    IReadOnlyList<ConfigurationDefinitionSourceContribution> GetDefinitionContributions(ConfigurationDefinition definition);

    /// <summary>
    /// Gets all managed configuration values supplied by each runtime source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Source inventories ordered by provider priority from highest to lowest.</returns>
    Task<IReadOnlyList<ConfigurationSourceInventory>> GetSourceInventoriesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a display-safe JSON file view for one source.
    /// </summary>
    /// <param name="sourceKey">The source key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source file view.</returns>
    Task<ConfigurationSourceFileView> GetSourceFileViewAsync(string sourceKey, CancellationToken cancellationToken);
}

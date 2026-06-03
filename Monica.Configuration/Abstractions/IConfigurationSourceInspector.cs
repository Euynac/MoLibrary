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
    /// Gets source contribution counts for a definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The contribution rows.</returns>
    IReadOnlyList<ConfigurationDefinitionSourceContribution> GetDefinitionContributions(ConfigurationDefinition definition);

    /// <summary>
    /// Gets all managed configuration values supplied by each runtime source.
    /// </summary>
    /// <returns>Source inventories ordered by provider priority from highest to lowest.</returns>
    IReadOnlyList<ConfigurationSourceInventory> GetSourceInventories();

    /// <summary>
    /// Gets a display-safe JSON file view for one source.
    /// </summary>
    /// <param name="sourceKey">The source key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source file view.</returns>
    Task<ConfigurationSourceFileView> GetSourceFileViewAsync(string sourceKey, CancellationToken cancellationToken);
}

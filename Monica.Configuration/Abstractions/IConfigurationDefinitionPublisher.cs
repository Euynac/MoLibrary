using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Publishes owner-scanned configuration definitions to a shared schema store.
/// </summary>
public interface IConfigurationDefinitionPublisher
{
    /// <summary>
    /// Publishes definitions.
    /// </summary>
    /// <param name="definitions">The definitions to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(IReadOnlyList<ConfigurationDefinition> definitions, CancellationToken cancellationToken);
}

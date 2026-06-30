using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Exposes Repository DbContext registrations captured during module configuration.
/// </summary>
public interface IRepositoryDbContextRegistry
{
    /// <summary>
    /// Gets all registered Repository DbContexts in registration order.
    /// </summary>
    /// <returns>The immutable list of registered DbContexts.</returns>
    IReadOnlyList<RepositoryDbContextRegistration> GetRegistrations();

    /// <summary>
    /// Finds a registered Repository DbContext by its stable context identifier.
    /// </summary>
    /// <param name="contextId">The context identifier.</param>
    /// <returns>The matching registration.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no context matches <paramref name="contextId"/>.</exception>
    RepositoryDbContextRegistration GetRequiredRegistration(string contextId);
}

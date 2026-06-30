using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Services.Support;

internal sealed class RepositoryDbContextRegistry(IEnumerable<RepositoryDbContextRegistration> registrations)
    : IRepositoryDbContextRegistry
{
    private readonly IReadOnlyList<RepositoryDbContextRegistration> _registrations = registrations
        .OrderBy(registration => registration.RegistrationOrder)
        .ThenBy(registration => registration.FullName, StringComparer.Ordinal)
        .ToList();

    public IReadOnlyList<RepositoryDbContextRegistration> GetRegistrations()
    {
        return _registrations;
    }

    public RepositoryDbContextRegistration GetRequiredRegistration(string contextId)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            throw new ArgumentException("DbContext identifier is required.", nameof(contextId));
        }

        return _registrations.FirstOrDefault(registration =>
                   string.Equals(registration.ContextId, contextId, StringComparison.Ordinal)
                   || string.Equals(registration.FullName, contextId, StringComparison.Ordinal)
                   || string.Equals(registration.DbContextName, contextId, StringComparison.Ordinal))
               ?? throw new KeyNotFoundException($"Repository DbContext '{contextId}' is not registered.");
    }
}

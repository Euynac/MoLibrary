using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.Persistence.Abstractions;

/// <summary>
/// Marks a DbContext that needs unit-of-work scope settings before it participates in repository operations.
/// </summary>
public interface IUnitOfWorkAwareDbContext
{
    /// <summary>
    /// Applies the active unit-of-work scope options to the DbContext.
    /// </summary>
    /// <param name="options">The options used by the active unit-of-work scope.</param>
    void Initialize(UnitOfWorkScopeOptions options);
}
